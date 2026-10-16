#!/usr/bin/env python3
"""Re-runnable, read-only measurement aggregation. Writes only this QA folder.

python3 QA/WeaponUpgradeBalance20261015/analyze_upgrades.py
Live CSVs are read as snapshots; incomplete trailing records are ignored.
"""
import collections
import csv
import datetime
import hashlib
import io
import json
import math
import statistics
from pathlib import Path

F = Path(__file__).resolve().parent
WEAPONS = ['GearCraft', 'SteamCannon', 'SteamThrower', 'RailCraft', 'SteamGatling']
BATCHES = ['route', 'probes', 'route-fix', 'fitted-final']
REPORT_BATCHES = ['route', 'probes', 'fitted-final']
KNOWN_BAD = ('route-results.csv', 'route_GearCraft_4')
FLOATS = ['seconds', 'damage', 'player_damage', 'gate_damage', 'boss_seconds',
          'boss_remaining_hp', 'average_game_dt']
INTS = ['stage', 'day', 'seed', 'total_including_summons', 'killed', 'escaped_or_suicided',
        'remaining', 'shots', 'frames', 'scheduled_count', 'scheduled_killed',
        'scheduled_escaped', 'scheduled_remaining', 'attack_events']


def main():
    provenance = {}
    warnings, exclusions = [], []

    def read_csv(name):
        path = F/name
        if not path.exists():
            provenance[name] = {'exists': False}
            return []
        data = path.read_bytes()
        trailing = not data.endswith(b'\n')
        complete = data[:data.rfind(b'\n')+1] if trailing else data
        reader = csv.DictReader(io.StringIO(complete.decode('utf-8-sig')))
        rows = []
        for line, row in enumerate(reader, 2):
            if None in row or any(v is None for v in row.values()):
                warnings.append(f'{name}:{line}: incomplete/malformed row skipped')
                continue
            rows.append(dict(row, _line=line))
        provenance[name] = {'exists': True, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                            'complete_rows': len(rows), 'ignored_partial_tail': trailing}
        return rows

    def plain(row):
        return {k:v for k,v in row.items() if not k.startswith('_')}

    loadouts={r['profile']:plain(r) for r in read_csv('loadouts.csv')}
    expected = {}
    for batch in BATCHES:
        for row in read_csv(batch+'-cases.csv'):
            item = plain(row)
            item.update(stage=int(row['stage']), seed=int(row['seed']), maxSeconds=float(row['maxSeconds']))
            item['batch'] = 'route' if batch == 'route-fix' else batch
            expected[row['id']] = item

    selected = {}
    for batch in BATCHES:
        filename = batch+'-results.csv'
        for raw in read_csv(filename):
            case_id = raw.get('id', '')
            source = {'file': filename, 'line': raw['_line']}
            if (filename, case_id) == KNOWN_BAD:
                exclusions.append(dict(id=case_id, source=source,
                    reason='Known duplicate native initial spawns. Never use this initial result; prefer route-fix.'))
                continue
            try:
                row = plain(raw)
                for key in FLOATS:
                    row[key] = float(row[key])
                    if not math.isfinite(row[key]): raise ValueError(key+' is not finite')
                for key in INTS: row[key] = int(row[key])
                metadata = expected.get(case_id)
                if metadata is None: raise ValueError('case missing from plan')
                if any(str(row[k]) != str(metadata[k]) for k in ['weapon', 'stage', 'seed', 'profile']):
                    raise ValueError('result identity differs from case plan')
                row.update(mode=metadata['mode'], max_seconds=metadata['maxSeconds'],
                           batch=metadata['batch'], source=source, source_batch=batch)
            except (KeyError, ValueError) as exc:
                exclusions.append(dict(id=case_id, source=source, reason='Invalid result: '+str(exc)))
                continue
            if case_id in selected:
                exclusions.append(dict(id=case_id, source=selected[case_id]['source'],
                    reason='Superseded by later result '+filename))
            selected[case_id] = row

    spawns, plans, stats = {}, {}, {}
    for batch in BATCHES:
        spawns[batch] = collections.defaultdict(list)
        plans[batch] = collections.defaultdict(list)
        stats[batch] = {}
        for row in read_csv(batch+'-spawns.csv'): spawns[batch][row['case']].append(plain(row))
        for row in read_csv(batch+'-spawn-plan.csv'): plans[batch][row['case']].append(plain(row))
        for row in read_csv(batch+'-loadout-stats.csv'): stats[batch][row['case']] = plain(row)

    for row in selected.values():
        batch, case_id = row['source_batch'], row['id']
        row['loadout_stats'] = stats[batch].get(case_id)
        row['module_damage'] = float(loadouts.get(row['profile'],{}).get('moduleDamage',0))
        row['is_module_bridge'] = row['module_damage'] > 0
        plan = plans[batch].get(case_id, [])
        signature = [(int(p['index']), p['scheduled_elapsed'], p['name'], p['last']) for p in plan]
        row['spawn_plan_sha256'] = hashlib.sha256(json.dumps(signature, separators=(',', ':')).encode()).hexdigest() if plan else None
        row['planned_count'] = len(plan) if plan else None
        boss_rows = [s for s in spawns[batch].get(case_id, []) if s['name'] == row['boss_name']]
        max_hp = max((float(s['maxhp']) for s in boss_rows), default=None)
        row['boss_initial_hp'] = max_hp
        row['boss_damage'] = max(0., min(max_hp, max_hp-row['boss_remaining_hp'])) if max_hp is not None else None
        boss_killed = bool(row['boss_name']) and row['boss_remaining_hp'] <= 0
        row['boss_killed'] = boss_killed
        row['boss_kill_seconds'] = row['boss_seconds'] if boss_killed and row['boss_seconds'] >= 0 else None
        row['boss_dps_observed'] = (row['boss_damage']/row['boss_seconds']
            if row['boss_damage'] is not None and row['boss_seconds'] > 0 else None)
        row['is_boss_window'] = row['mode'] == 'boss' and row['max_seconds'] <= 60.01
        row['window_status'] = ('killed_before_window_end' if boss_killed else
            'window_censored' if row['result'] == 'timeout' else 'other_'+row['result']) if row['is_boss_window'] else None
        if row['mode']=='wave' and row['stage'] not in [9,18,30,31] and row['total_including_summons'] != row['scheduled_count']:
            warnings.append(f"{case_id}: normal-wave observed enemy count {row['total_including_summons']} differs from schedule {row['scheduled_count']}; inspect contamination")
        if row['loadout_stats'] and float(row['loadout_stats']['module_damage']) != row['module_damage']:
            warnings.append(case_id+': actual module_damage differs from configured profile')

    order = lambda r:(r['stage'], WEAPONS.index(r['weapon']), r['mode'], r['seed'], r['profile'])
    rows = sorted(selected.values(), key=order)
    route = [r for r in rows if r['batch']=='route' and r['mode']=='wave']

    def aggregate(records, expected_stages):
        stages = sorted(r['stage'] for r in records)
        missing = sorted(set(expected_stages)-set(stages))
        all_clear = not missing and all(r['result']=='cleared' for r in records)
        return dict(observed=len(records), expected=len(expected_stages), missing_stages=missing,
            cleared=sum(r['result']=='cleared' for r in records), complete_clear=all_clear,
            clear_seconds_sum=sum(r['seconds'] for r in records) if all_clear else None,
            observed_seconds_sum=sum(r['seconds'] for r in records),
            scheduled=sum(r['scheduled_count'] for r in records),
            scheduled_killed=sum(r['scheduled_killed'] for r in records),
            scheduled_escaped=sum(r['scheduled_escaped'] for r in records),
            player_damage=sum(r['player_damage'] for r in records),
            gate_damage=sum(r['gate_damage'] for r in records),
            profiles=sorted(set(r['profile'] for r in records)), cases=[r['id'] for r in records])

    normal_interval = {w:aggregate([r for r in route if r['weapon']==w and 19<=r['stage']<=29], list(range(19,30))) for w in WEAPONS}
    full_routes = {}
    for w in WEAPONS:
        stages = sorted(m['stage'] for m in expected.values() if m['batch']=='route' and m['weapon']==w)
        full_routes[w] = aggregate([r for r in route if r['weapon']==w], stages)
    route_bosses = [r for r in route if r['stage'] in [9,18,30,31]]
    boss_windows = [r for r in rows if r['is_boss_window']]

    same_profile_groups = collections.defaultdict(list)
    same_weapon_groups = collections.defaultdict(list)
    for r in rows:
        if r['batch']=='fitted-final':continue  # Separate limited replacement comparison.
        same_profile_groups[(r['stage'],r['seed'],r['profile'],r['mode'])].append(r)
        same_weapon_groups[(r['weapon'],r['stage'],r['seed'],r['mode'])].append(r)
    comparable = []
    for key, members in same_profile_groups.items():
        if len(set(r['weapon'] for r in members)) < 2: continue
        signatures={r['spawn_plan_sha256'] for r in members if r['spawn_plan_sha256']}
        plan_match = len(signatures)==1 if signatures else None
        if plan_match is False: warnings.append('Spawn-plan mismatch for same stage/seed/profile: '+str(key))
        comparable.append(dict(stage=key[0], seed=key[1], profile=key[2], mode=key[3],
            spawn_plans_match=plan_match, cases=[r['id'] for r in members]))

    candidate_comparisons=[]
    for key,members in same_weapon_groups.items():
        if len(set(r['profile'] for r in members))<2:continue
        signatures={r['spawn_plan_sha256'] for r in members if r['spawn_plan_sha256']}
        baseline=next((r for r in members if r['batch']=='route'),None)
        if baseline is None: baseline=next((r for r in members if r['profile']=='plate_only'),None)
        entries=[]
        for r in members:
            seconds_change=(100*(r['seconds']/baseline['seconds']-1)
                if baseline and r['mode']=='wave' and baseline['result']==r['result']=='cleared' and baseline['seconds']>0 else None)
            entries.append(dict(id=r['id'],profile=r['profile'],seconds=r['seconds'],result=r['result'],
                delta_seconds_percent_vs_representative=seconds_change, player_damage=r['player_damage'],
                gate_damage=r['gate_damage'],boss_dps_observed=r['boss_dps_observed'],window_status=r['window_status'],
                module_damage=r['module_damage'],is_additional_module_spend=r['is_module_bridge']))
        candidate_comparisons.append(dict(weapon=key[0],stage=key[1],seed=key[2],mode=key[3],
            representative_baseline=baseline['id'] if baseline else None,
            spawn_plans_match=len(signatures)==1 if signatures else None,entries=entries))

    seed28={}
    for w in WEAPONS:
        meta=next((m for m in expected.values() if m['batch']=='route' and m['weapon']==w and m['stage']==28),None)
        wanted=meta['profile'] if meta else None
        records=[r for r in rows if r['weapon']==w and r['stage']==28 and r['mode']=='wave' and r['profile']==wanted and r['seed'] in [77,78,79]]
        seeds=sorted(set(r['seed'] for r in records));complete=seeds==[77,78,79] and all(r['result']=='cleared' for r in records)
        values=[r['seconds'] for r in records]
        seed28[w]=dict(profile=wanted,seeds=seeds,complete_three_seed_clear=complete,
            mean_seconds=statistics.mean(values) if complete else None,
            sd_seconds=statistics.stdev(values) if complete else None,
            min_seconds=min(values) if complete else None,max_seconds=max(values) if complete else None,
            samples=[dict(seed=r['seed'],seconds=r['seconds'],result=r['result'],player_damage=r['player_damage'],gate_damage=r['gate_damage'],id=r['id']) for r in records])

    budgets={r['profile']:plain(r) for r in read_csv('loadout-budgets.csv')}
    validation={r['profile']:plain(r) for r in read_csv('native-purchase-validation.csv')}
    audit_path=F/'upgrade-parts-audit.json'
    if audit_path.exists():
        audit_bytes=audit_path.read_bytes()
        audit=json.loads(audit_bytes)
        provenance[audit_path.name]={'exists':True,'bytes':len(audit_bytes),'sha256':hashlib.sha256(audit_bytes).hexdigest()}
    else: audit={'builds':[]}
    audited={b['name']:b for b in audit['builds']}
    fitted_path=F/'fitted-final-loadouts.json'
    if fitted_path.exists():
        fitted_bytes=fitted_path.read_bytes()
        provenance[fitted_path.name]={'exists':True,'bytes':len(fitted_bytes),'sha256':hashlib.sha256(fitted_bytes).hexdigest()}
        audited.update({p['name']:p for p in json.loads(fitted_bytes)['profiles']})
    costs={}
    for name,loadout in loadouts.items():
        module_damage=float(loadout['moduleDamage'])
        base_name=name.removesuffix('_module20') if name.endswith('_module20') else name
        native=validation.get(name,validation.get(base_name,{}));a=audited.get(name,audited.get(base_name,{}))
        module_count=4 if module_damage==20 else 0
        extra_costs={'UpgradeCore':module_count*3,'ModuleCore_lv2':module_count} if module_count else {}
        costs[name]=dict(parts=loadout['parts'].split(';') if loadout['parts'] else [],module_damage=module_damage,
            material_budget={m:int(budgets[name][m]) for m in ['Scrap','Gear','UpgradeCore']} if name in budgets else None,
            native_purchase_success=int(native['parts'])==int(native['purchased']) if native else None,
            native_validation_source_profile=native.get('profile'),native_validation_inherited_parts_only=name not in validation and base_name in validation,
            native_part_costs={m:int(native[k]) for m,k in [('Scrap','spent_scrap'),('Gear','spent_gear'),('UpgradeCore','spent_core')]} if native else None,
            reported_part_costs={m:int(native[k]) for m,k in [('Scrap','spent_scrap'),('Gear','spent_gear'),('UpgradeCore','spent_core')]} if native else a.get('part_costs'),
            cost_verification_source='native_purchase' if native else 'static_audit' if a.get('part_costs') else 'unavailable',
            native_effects=native,shop_witness=a.get('shop',{}).get('witness'),
            estimated_same_day_collection_fraction=a.get('shop',{}).get('success_fraction'),
            additional_module_costs=extra_costs,module_recipe_unlock_boss_kills=2 if module_count else None,
            budget_category='parts_plus_separate_module_spend' if module_count else 'parts_only',
            note='Material budget is for parts only. Module20 additionally costs UpgradeCore12 + ModuleCore_lv2 x4 (unlock2), NOT equal total cost to module0. Native part costs exclude rerolls; shop witness total includes rerolls. Inherited validation proves identical part placements only, not module20 runtime stats.' if module_count else
                 'Native part costs exclude rerolls; shop witness total includes rerolls. Route measurements do not simulate acquisition income or shop visits.')

    module_bridges=[]
    for r in boss_windows:
        if not r['is_module_bridge']:continue
        base_name=r['profile'].removesuffix('_module20')
        base=next((b for b in boss_windows if b['weapon']==r['weapon'] and b['stage']==r['stage'] and b['seed']==r['seed'] and b['profile']==base_name),None)
        module_bridges.append(dict(weapon=r['weapon'],stage=r['stage'],seed=r['seed'],module_case=r['id'],
            parts_only_case=base['id'] if base else None,additional_module_costs=costs[r['profile']]['additional_module_costs'],
            module_dps=r['boss_dps_observed'],parts_only_dps=base['boss_dps_observed'] if base else None,
            observed_dps_ratio=(r['boss_dps_observed']/base['boss_dps_observed'] if base and r['boss_dps_observed'] is not None and base['boss_dps_observed'] else None),
            equal_total_budget=False))

    fitted_final=[]
    for r in rows:
        if r['batch']!='fitted-final':continue
        baseline=next((b for b in route if b['weapon']==r['weapon'] and b['stage']==r['stage'] and b['seed']==r['seed']),None)
        same_module=baseline is not None and baseline['module_damage']==r['module_damage']
        paired=next((b for b in rows if b['batch']=='fitted-final' and b['weapon']==r['weapon'] and b['stage']==r['stage'] and b['seed']==r['seed'] and b['profile']==r['profile'].removesuffix('_module20')),None) if r['is_module_bridge'] else None
        fitted_final.append(dict(id=r['id'],weapon=r['weapon'],stage=r['stage'],profile=r['profile'],module_damage=r['module_damage'],
            original_representative_case=baseline['id'] if baseline else None,
            same_module_as_original=same_module,
            delta_seconds_percent_vs_original=(100*(r['seconds']/baseline['seconds']-1) if same_module and baseline['result']==r['result']=='cleared' and baseline['seconds']>0 else None),
            paired_fitted_module0_case=paired['id'] if paired else None,
            additional_module_costs=costs[r['profile']]['additional_module_costs'],
            scope='Known ineffective Nozzle slots replaced, not an optimal-loadout search. Original representative route remains unchanged.'))

    pending={batch:sorted(i for i,m in expected.items() if m['batch']==batch and i not in selected) for batch in REPORT_BATCHES}
    result=dict(generated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        methodology='Native offensive throughput, per-wave reset, HP/GATE replenished; main comparison module0. Module20 bridge is separate additional spending, not an equal-total-budget comparison. Representative loadouts are not asserted optimal. Boss60 timeout means observation-window censoring, not ordinary clear failure.',
        planned_counts={batch:sum(m['batch']==batch for m in expected.values()) for batch in REPORT_BATCHES},
        input_snapshots=provenance,excluded_records=exclusions,warnings=warnings,pending=pending,
        records=rows,normal_19_29=normal_interval,representative_routes=full_routes,route_bosses=route_bosses,
        same_stage_seed_profile=comparable,candidate_comparisons=candidate_comparisons,
        boss60_windows=boss_windows,module20_bridges=module_bridges,fitted_final_comparisons=fitted_final,wave28_three_seeds=seed28,parts=costs)
    (F/'upgrade-analysis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    write_tables(result)
    print(f"Aggregated {len(route)}/{result['planned_counts']['route']} route, {sum(r['batch']=='probes' for r in rows)}/{result['planned_counts']['probes']} probes, {len(fitted_final)}/{result['planned_counts']['fitted-final']} fitted-final; exclusions={len(exclusions)}, warnings={len(warnings)}")
    print(str(F/'tables.txt'))


def number(value, digits=2):
    return '未収集' if value is None else f'{value:.{digits}f}'


def write_tables(result):
    rows=result['records'];by_id={r['id']:r for r in rows}
    lines=['アップグレード比較・途中集計 '+result['generated_utc'],
        '主比較Module0。Module20は追加素材を使う別比較。実パーツから計算。HP/GATE補充・wave毎リセットで、通常の生存クリア時間ではない。',
        '取得後～31は代表構成の測定。候補選定より先に実施しており、最適構成とは主張しない。',
        f"収集済 route={sum(r['batch']=='route' for r in rows)}/{result['planned_counts']['route']}、probes={sum(r['batch']=='probes' for r in rows)}/{result['planned_counts']['probes']}、fitted-final={len(result['fitted_final_comparisons'])}/{result['planned_counts']['fitted-final']}。未完全集計の全区間合計は出さない。",'']
    lines+=['19～29：共通の11通常wave（代表構成。武器別profileは異なる）',
            '武器             完了/予定  秒合計     予定敵撃破/数  門逸走等  HP被害  門被害']
    for w,a in result['normal_19_29'].items():
        lines.append(f"{w:16} {a['cleared']:2}/{a['expected']:<2} {number(a['clear_seconds_sum']):>10} {a['scheduled_killed']:4}/{a['scheduled']:<4} {a['scheduled_escaped']:5} {a['player_damage']:7.1f} {a['gate_damage']:7.1f}")
    lines+=['','取得段階～31：代表構成ルート（取得時刻、工房/UI/ED時間を含まない）']
    for w,a in result['representative_routes'].items():
        lines.append(f"{w:16} {a['cleared']}/{a['expected']}、全区間秒={number(a['clear_seconds_sum'])}、未測定wave={','.join(map(str,a['missing_stages'])) or 'なし'}")
    lines+=['','ルートのボス：出現～撃破秒／wave秒／最終残HP／HP・門被害']
    for r in result['route_bosses']:
        lines.append(f"{r['weapon']:12} w{r['stage']:02} {r['profile']:24} boss={number(r['boss_kill_seconds'])} wave={r['seconds']:.2f} 残HP={r['boss_remaining_hp']:.1f} HP/GATE={r['player_damage']:.1f}/{r['gate_damage']:.1f} [{r['result']}]")
    lines+=['','同wave・同seed・同profileの武器比較（代表wave抜粋。全群はJSON。秒、HP/門被害）']
    for g in result['same_stage_seed_profile']:
        if g['mode']!='wave' or g['stage'] not in [9,10,18,19,28,31]:continue
        members=[by_id[i] for i in g['cases']]
        text=' / '.join(f"{r['weapon']} {r['seconds']:.2f}s ({r['player_damage']:.0f}/{r['gate_damage']:.0f})" for r in members)
        lines.append(f"w{g['stage']} seed{g['seed']} {g['profile']} plan一致={g['spawn_plans_match']}: {text}")
    lines+=['','候補比較：同武器・wave・seed、%は代表ルート秒との差（負値が短縮）']
    for g in result['candidate_comparisons']:
        if g['mode']!='wave':continue
        entries=[]
        for e in g['entries']:
            entries.append(f"{e['profile']} {e['seconds']:.2f}s Δ{number(e['delta_seconds_percent_vs_representative'],1)}% HP/GATE={e['player_damage']:.0f}/{e['gate_damage']:.0f} [{e['result']}]")
        lines.append(f"{g['weapon']} w{g['stage']} seed{g['seed']}: "+' / '.join(entries))
    lines+=['','EK単体60秒観測：ボスHP減少量÷実観測秒。未撃破は窓打切で、クリア失敗とは数えない。',
            '早期撃破時は撃破までの平均DPS。秒間総ダメージに召喚敵ダメージを混ぜない。']
    for r in result['boss60_windows']:
        if r['is_module_bridge']:continue
        state='撃破' if r['boss_killed'] else '60秒窓打切' if r['window_status']=='window_censored' else r['window_status']
        lines.append(f"{r['weapon']:12} {r['profile']:25} DPS={number(r['boss_dps_observed'])} damage={number(r['boss_damage'],1)} 観測={r['boss_seconds']:.3f}s 残HP={r['boss_remaining_hp']:.1f} {state}")
    lines+=['','Module20接続確認（追加U12＋中級ModuleCore4、解放はボス2体。パーツ予算とは別支出）']
    for g in result['module20_bridges']:
        r=by_id[g['module_case']]
        state='撃破' if r['boss_killed'] else '60秒窓打切' if r['window_status']=='window_censored' else r['window_status']
        lines.append(f"{g['weapon']:12} partsのみDPS={number(g['parts_only_dps'])} → Module20={number(g['module_dps'])} 比={number(g['observed_dps_ratio'])} 観測={r['boss_seconds']:.3f}s 残HP={r['boss_remaining_hp']:.1f} {state}（総費用は同等ではない）")
    lines+=['','既知の無効枠交換・最終wave追加比較（元routeは維持。最適解の主張なし）']
    for g in result['fitted_final_comparisons']:
        r=by_id[g['id']]
        delta=f"元route比 {number(g['delta_seconds_percent_vs_original'],1)}%" if g['same_module_as_original'] else 'Module20追加支出、元routeと同予算比較不可'
        lines.append(f"{g['weapon']:12} {g['profile']} wave={r['seconds']:.2f}s boss={number(r['boss_kill_seconds'])} 残HP={r['boss_remaining_hp']:.1f} HP/GATE={r['player_damage']:.0f}/{r['gate_damage']:.0f} {delta} [{r['result']}]")
    lines+=['','wave28 代表構成・3seed（77/78/79、平均±標本SD、範囲）']
    for w,g in result['wave28_three_seeds'].items():
        samples=', '.join(f"{r['seed']}:{r['seconds']:.2f}s" for r in g['samples'])
        lines.append(f"{w:16} n={len(g['seeds'])}/3 mean={number(g['mean_seconds'])} sd={number(g['sd_seconds'])} [{number(g['min_seconds'])},{number(g['max_seconds'])}] ({samples})")
    lines+=['','パーツ購入費：原価 S/G/U（再抽選を除く）／上限 S/G/U／取得抽選例の総費用']
    for name,p in result['parts'].items():
        fmt=lambda d:'/'.join(str(d.get(m,0)) for m in ['Scrap','Gear','UpgradeCore']) if d else '-'
        witness=p['shop_witness'];total=witness['total_costs'] if witness else None
        rate=p['estimated_same_day_collection_fraction']
        extra=' ＋別支出U12/中級Core4' if p['additional_module_costs'] else ''
        validation_label='基profileの配置のみ' if p['native_validation_inherited_parts_only'] else str(p['native_purchase_success'])
        lines.append(f"{name:25} {fmt(p['reported_part_costs']):>10} / {fmt(p['material_budget']):>10} / {fmt(total):>10} 購入配置={validation_label} 費用根拠={p['cost_verification_source']} 当日収集推定={number(100*rate,1)+'%' if rate is not None else '-'}{extra}")
    lines+=['','データ採用履歴（詳細な行番号・入力SHA256は upgrade-analysis.json）']
    for e in result['excluded_records']:lines.append(f"除外 {e['source']['file']}:{e['source']['line']} {e['id']}: {e['reason']}")
    fix=by_id.get('route_GearCraft_4')
    lines.append('route_GearCraft_4 の採用元: '+(fix['source']['file'] if fix else '未測定扱い。route-fix待ち。'))
    lines += ['注意: '+w for w in result['warnings']]
    (F/'tables.txt').write_text('\n'.join(lines)+'\n')


if __name__=='__main__':main()
