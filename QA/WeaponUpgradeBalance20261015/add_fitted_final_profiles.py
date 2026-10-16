#!/usr/bin/env python3
"""QA-only additive profiles: replace known ineffective Nozzles; no game edits."""
import collections
import csv
import hashlib
import io
import json
import math
from pathlib import Path

F=Path(__file__).resolve().parent


def read(name):
    with (F/name).open(newline='') as f:return list(csv.DictReader(f))


def append_unique(name,new_rows):
    current=read(name);fields=list(current[0]);profiles={r['profile'] for r in current};todo=[]
    for profile in dict.fromkeys(r['profile'] for r in new_rows):
        wanted=[r for r in new_rows if r['profile']==profile]
        if profile in profiles:
            assert [r for r in current if r['profile']==profile]==wanted,(name,profile,'conflict')
        else:todo+=wanted
    if todo:
        buffer=io.StringIO(newline='');writer=csv.DictWriter(buffer,fieldnames=fields);writer.writerows(todo)
        with (F/name).open('ab') as out:out.write(buffer.getvalue().encode())


def main():
    guarded=['cases.csv','probes-cases.csv','route-cases.csv','batch-name.txt']
    before={n:hashlib.sha256((F/n).read_bytes()).hexdigest() for n in guarded if (F/n).exists()}
    loadouts=read('loadouts.csv');placements=read('loadout-placements.csv');budgets=read('loadout-budgets.csv')
    base='final_day11_ranged';source=next(r for r in loadouts if r['profile']==base)
    source_places=[r for r in placements if r['profile']==base];source_budget=next(r for r in budgets if r['profile']==base)
    parts={p['asset']:p for p in json.loads((F/'upgrade-parts-audit.json').read_text())['catalog']}
    extra_loadouts=[];extra_places=[];extra_budgets=[];details=[]
    for target,thrower in [('final_day11_ranged_fitted_speed',False),('final_day11_thrower_fitted_damage',True)]:
        fitted=[];nozzles=0;speed_swapped=False
        for original in source_places:
            row=dict(original,profile=target);asset=row['asset']
            if asset=='PrecisionNozzle':
                nozzles+=1
                if thrower:
                    row['asset']='Damage+2' if nozzles==1 else 'Damage+1'
                    row['rotation']='0'  # Both original Nozzles are horizontal at rotation1.
                else:row['asset']='SpeedGear'  # Same 1x2 shape, same rotation.
            elif thrower and asset=='SpeedGear+' and not speed_swapped:
                row['asset']='SpeedGear';speed_swapped=True
            fitted.append(row)
        assert nozzles==2 and (speed_swapped or not thrower)
        occupied=set();costs=collections.Counter();flat=0.;speed=1.;double=False;spread=0.
        for row in fitted:
            part=parts[row['asset']];assert part['shop_registered'];costs.update(part['costs']);shape=part['shape']
            for _ in range(int(row['rotation'])):shape=[list(r) for r in zip(*shape[::-1])]
            cells={(int(row['x'])+x,int(row['y'])+y) for y,r in enumerate(shape) for x,v in enumerate(r) if v}
            assert all(0<=x<6 and 0<=y<6 for x,y in cells);assert not occupied&cells;occupied|=cells
            for e in part['effects']:
                if e['type']==0:flat+=e['value']
                elif e['type']==1:speed*=e['value']
                elif e['type']==2:double=True
                elif e['type']==3:spread-=e['value']
        assert all(costs[m]<=int(source_budget[m]) for m in costs)
        expected={'Scrap':95,'Gear':13,'UpgradeCore':6} if thrower else {'Scrap':96,'Gear':14,'UpgradeCore':6}
        assert dict(costs)==expected,(target,costs)
        assert len(occupied)==(33 if thrower else 34)
        for module in [0,20]:
            name=target+('_module20' if module else '')
            members=[dict(r,profile=name) for r in fitted]
            extra_places+=members
            extra_loadouts.append(dict(source,profile=name,parts=';'.join(r['asset'] for r in members),moduleDamage=str(module)))
            extra_budgets.append(dict(source_budget,profile=name))
            details.append(dict(name=name,source_profile=base,weapons=['SteamThrower'] if thrower else ['SteamCannon','RailCraft'],
                grid_size=6,occupied_cells=len(occupied),parts=[r['asset'] for r in members],
                placements=[dict(asset=r['asset'],gridX=int(r['x']),gridY=int(r['y']),rotation=int(r['rotation'])) for r in members],
                part_costs=dict(costs),parts_budget={m:int(source_budget[m]) for m in ['Scrap','Gear','UpgradeCore']},
                effects=dict(flat_damage=flat,attack_interval_multiplier=speed,bullet_double=double,spread_modifier=spread),
                module_damage=module,additional_module_costs={'UpgradeCore':12,'ModuleCore_lv2':4} if module else {},
                module_unlock_boss_kills=2 if module else None,
                change='Nozzle x2 -> Damage+2 + Damage+1; one SpeedGear+ -> SpeedGear' if thrower else 'Nozzle x2 -> SpeedGear x2',
                verification='Static occupancy/shape/cost check only. Existing final placements retained; native purchase and combat are parent-run. No claim of globally optimal build. Module costs are separate from parts budget.'))
    append_unique('loadouts.csv',extra_loadouts);append_unique('loadout-placements.csv',extra_places);append_unique('loadout-budgets.csv',extra_budgets)
    (F/'fitted-final-loadouts.json').write_text(json.dumps(dict(kind='QA profiles replacing known ineffective zero-spread Nozzles',profiles=details),ensure_ascii=False,indent=2)+'\n')
    assert all(hashlib.sha256((F/n).read_bytes()).hexdigest()==h for n,h in before.items())
    for p in details:print(p['name'],p['part_costs'],p['occupied_cells'],p['effects'])
    print('Active case plans and batch-name unchanged.')


if __name__=='__main__':main()
