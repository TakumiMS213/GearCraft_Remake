#!/usr/bin/env python3
"""Render the completed v2 matrix after this session has been restored."""
import json
from pathlib import Path

q = Path(__file__).resolve().parent
j = json.loads((q / 'results.json').read_text())
assert j['completed_trials'] == j['planned_trials'] == 60
assert (q / 'canonical-restoration.txt').exists(), 'Restore the current session first'
groups = j['normal_conditions'] + j['hypothetical_locked_conditions']
assert len(groups) == 20
assert all(g['completed_trials'] == 3 for g in groups)
assert all(t['label'].endswith('_v2') and not t['warnings'] for g in groups for t in g['trials'])
by = {(g['boss'], g['weapon']): g for g in groups}
weapons = ['GearCraft', 'SteamCannon', 'SteamThrower', 'RailCraft', 'SteamGatling']
bosses = ['IC408', 'BB413', 'AR227', 'EK9']
names = {'IC408': 'IC-408', 'BB413': 'BB-413', 'AR227': 'AR-227', 'EK9': 'EK-9'}
outcome = {'defeated': '生存撃破', 'player_dead': '死亡', 'gate_destroyed': '門破壊', 'timeout': '時間切れ'}

def fmt(n):
    return '—' if n is None else f'{n:.2f}'

def cell(g):
    n = g['successful_trials']
    s = f"{fmt(g['kill_seconds_successes_only']['median'])}秒 ({n}/3)" if n else '未撃破 (0/3)'
    return s + (' ※' if not g['normally_available'] else '')

lines = [
    '工房武装5種 × 全ボス4種　実ゲーム比較レポート',
    '2026-10-21 / 採用結果: v2 / 各3 seed（77・101・202）、計60試行',
    '',
    '生存撃破の時間と、防衛失敗を分けて評価した。武器・ボスの製品パラメータは変更していない。',
    '時間はボス生成要求からのゲーム内秒で、画面への侵入・接近・スタンを含む。表は生存撃破した試行の中央値。',
    '括弧は生存撃破件数/3。死亡・門破壊までの時間は討伐時間に含めない。',
    '※は通常その時点では未解放の武器を仮支給した参考条件。通常進行の調整判断から分ける。',
    '',
    '武器 | IC-408 | BB-413 | AR-227 | EK-9',
]
for w in weapons:
    lines.append(w + ' | ' + ' | '.join(cell(by[b, w]) for b in bosses))

lines += ['', '各武器の評価', '']
lines += [
    f"GearCraft：序盤ICは{cell(by['IC408','GearCraft'])}。BB・ARでは今回の接近/迎撃方針で生存撃破に至らず、EKは撃破前に門が破壊された。本体の門到達が原因とは確認されていない。",
    'ARでは攻撃受付が831〜857回あるが、これは実命中数ではない。射程外で見送るフレームは8.3〜11.0%、突進中に受けた5ダメージが各試行55〜60HPを占めた。',
    '初期武器としての役割は成立する一方、後半の近接防衛には課題が残る。接近・迎撃・退避の手動確認を伴わずに火力不足だけと決めない。',
    '',
    f"SteamCannon：BBは{cell(by['BB413','SteamCannon'])}。ARは0/3、EKは{cell(by['EK9','SteamCannon'])}。",
    'BBでの安定性に比べ、後半では生存面の余裕が小さい。ARはボス残HP約0.12%、EKは約0.80%まで削って死亡した試行もある。',
    '撃破に必要な時間だけでなく、その間の回避・迎撃負担を先に確認する。直撃と爆発の重複ヒットも実処理に含まれている。',
    '',
    f"SteamThrower：BB・ARは0/3。EKは{cell(by['EK9','SteamThrower'])}。単独で持ち続ける運用として測定した。",
    '減速付与は正常。ARのseed77では非スタン時間の約99.8%に減速がかかり、動作中の移動Tweenは0.55倍だった。通常射撃タイマーそのものは遅くならない。',
    'BB強弾への迎撃APIは接続されているが、低い接触ダメージとBOTの退避判断が重なり死亡した可能性が高い。減速後に別武器へ持ち替える支援運用は未計測であり、今回だけでデバフ役の価値や再強化の必要性を確定できない。',
    '',
    f"RailCraft：通常入手可能なARでは0/3、EKは{cell(by['EK9','RailCraft'])}。ACC×10の強化を含む。",
    '貫通火力の高さだけで防衛の安定性は保証されない。ARでは攻撃列、照準、射撃待ち時間と回避が影響する。',
    'BBの参考条件では1回409ダメージが強弾HP20を上回っていても死亡しており、迎撃失敗を単純な威力不足として扱えない。',
    '',
    f"SteamGatling：ARは{cell(by['AR227','SteamGatling'])}、EKは{cell(by['EK9','SteamGatling'])}。ARでは今回唯一3試行とも生存撃破した。",
    '連続射撃・二連射・攻撃力20モジュールを含む構成で評価した。近い入手段階のRailCraftとの差を優先的に再確認する。単純なACC倍率の低さだけでは実効火力を評価できない。',
    'AR・EK構成は60Hz条件の連射上限に達している。攻撃速度パーツの追加効果と、ダメージ/精度に割く余地は別途比較する。',
]

lines += ['', '調整判断', '',
    '優先度1：AR戦。今回の構成・BOTではGatlingと他4武器の差が大きい。ボスHPだけを増やす方向には進めず、迎撃・退避を含む生存余地と武器ごとの有効打を再確認する。',
    '優先度2：EK戦。生存撃破できた武器の時間と、死亡/門破壊した武器の残HPを併せて見る。失敗した武器を「最低60秒を達成」と判定しない。',
    '優先度3：SteamThrowerの支援運用。同一武器構成で、減速を付けて主武器へ戻す条件と主武器のみの対照を取る。今回の維持率だけでは何秒の防衛猶予を得たかは算出できない。',
    '今回の結果は調整候補を特定する比較であり、どの武器も人間には勝てないという結論ではない。製品パラメータの変更は行っていない。',
    '', '比較条件', '',
    'IC：3日目、当日スケール7（通常ボス出現stage9）、STR/ACC各4、モジュール攻撃力0、3×3の初期部品構成。',
    'BB：6日目、当日スケール16（通常stage18）、STR/ACC各10、モジュール攻撃力0、4×4の中盤部品構成。',
    'AR：10日目、当日スケール28（通常stage30）、STR/ACC各18、モジュール攻撃力20、5×5の近接/遠隔構成。',
    'EK：11日目、当日スケール31、STR/ACC各20、モジュール攻撃力20、6×6の武器別構成。',
    'プロフィール名に残るday4/day7は識別名であり、実際の日数とは異なる。詳細はmatrix-conditions.json / loadouts.csv / loadout-placements.csv。',
    '全条件HP100・門HP100から開始。PunkDriveとスクラップモジュールを併用。リペアはBB以降、バリアはAR以降。BOOST用Gear2を支給し、製品処理で消費する。全60試行で実際のBOOST使用2回をログ確認済み。',
    'パーツ配置・種類・個数を検証済み。STR/ACC、パーツ、モジュールの倍率は製品計算を使用。GearCraftの変形は製品の変形・5秒復帰・5秒待ちを通す。',
    '各段階の比較用構成であり、プレイヤーの平均所持数・店の乱数・購入予算・最適構成を実際の通しプレイから統計的に求めたものではない。',
    '', '初期の基礎ヒット値（装甲・二連射・爆発・継続ヒット・PunkDriveを加算する前）', '',
    '武器 | IC | BB | AR | EK',
    'GearCraft 剣/斧 | 50/70 | 59/79 | 90/110 | 92/112',
    'SteamCannon | 65 | 72.2 | 101.8 | 102.6',
    'SteamThrower | 0.540 | 0.756 | 4.444 | 4.564',
    'RailCraft | 346 | 409 | 513 | 532',
    'SteamGatling | 10.50 | 10.95 | 31.55 | 31.60',
    '遠距離武器はSTRではなくACCを使用。RailはACC×10、ThrowerはACC/パーツ×0.024、モジュール×0.17。モジュール20による加算はThrowerだけ3.4、ほかは20。',
    '内部名Damage+2の現アセット加算値は+3。上表は名前から推測せず実値を使用。単発値だけでは総ダメージ/DPSにならない。',
    '', '20条件の詳細（成功時間範囲、失敗時刻、残HP）', '']
for b in bosses:
    for w in weapons:
        g = by[b, w]
        k = g['kill_seconds_successes_only']
        lines.append(f"{names[b]} × {w} [{'通常入手可能' if g['normally_available'] else '未解放仮支給'}] {cell(g)} / 成功時間範囲 {fmt(k['min'])}〜{fmt(k['max'])}秒")
        for t in g['trials']:
            seconds = t['kill_seconds'] if t['outcome'] == 'defeated' else t['failure_seconds']
            remain = 100 * max(0, t['remaining_boss_hp']) / t['start_boss_hp']
            lines.append(f"  seed{t['seed']}: {outcome[t['outcome']]} {fmt(seconds)}秒 / 本人HP {fmt(t['player_hp'])} / 門HP {fmt(t['gate_hp'])} / ボス残HP {fmt(max(0,t['remaining_boss_hp']))} ({remain:.2f}%)")
        slow = g['slowed_nonstun_fraction']
        lines.append(f"  非スタン時減速維持率: {'—' if slow is None else f'{slow*100:.2f}%'} / スタン秒中央値 {fmt(g['stunned_seconds']['median'])}")

lines += ['', '測定の限界と検証記録', '',
    'Unity Editor上で、製品のAI・当たり判定・攻撃・スタン・召喚を動かす比較。前座を省き、初期装備を設定した単独ボス戦であり、全日程を進む通しプレイではない。',
    'プレイヤー判断は一定のBOT方針が代行。攻撃は製品ProcessAttackInputを毎ゲームフレーム1回通す。敵への直接ダメージ注入、HP/門の補充、デバッグ無敵は使わない。',
    'BBでBOTが退避できない場面など、武器固有の最適操作を再現できない制約がある。3/3成功は人間の成功率100%を意味せず、0/3も人間には不可能を意味しない。',
    '60Hzのゲーム時刻、16:9・480×270の観測。配布ビルドの性能やUI視認性のテストではない。集計時間は実際の処理待ちの壁時計時間ではない。',
    '最初のv1計測は、前座を省いたため移動中Spawnerの奥行きZを継承し、BB強弾/召喚とARミサイルを遅くした。全v1をexcluded-v1-transient-spawn-zへ隔離し、最終表から除外した。',
    'v2は既存Spawnerの両端点とプレイヤーが同じ平面にあることを検証し、生成Zだけを通常前座後の平面へ揃えた。製品AIのXY配置・移動はそのまま使用。Z観測とBB強弾速度5で確認。',
    '製品コードにはVector3正規化後Vector2化する経路が残る。通常ボスは前座後に大きいZ差が解消するが、初期雑魚や別配置への影響は別途確認候補。今回のQA条件修正と製品バグ修正を混同しない。',
    'PunkDriveを含む総ダメージで評価しており、主武器単体の寄与や命中率は分解していない。被弾元IDも未記録のため、死因の弾種はログとソースが整合する推定として記載した。',
    '今回開始直前に退避した16キーの保存状態・Editor設定を復元済み。証跡: canonical-restoration.txt。過去日のセーブ退避は復元に使っていない。',
    '結果データ: results.json / matrix-summary.csv。方法: methodology-audit.txt。最終照合: final-validation.json。',
]
(q / 'REPORT-ja.txt').write_text('\n'.join(lines) + '\n')
print('Wrote REPORT-ja.txt from 60 completed v2 trials after current-session restoration.')
