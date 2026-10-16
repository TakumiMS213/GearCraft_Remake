#!/usr/bin/env python3
"""Read-only source audit + offline packing/shop/economy model; no Unity execution.
Run: python3 QA/WeaponUpgradeBalance20261015/upgrade_parts_audit.py
Outputs only beside this file. Python RNG is not Unity RNG.
"""
import collections
import hashlib
import json
import math
import random
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
PARTS = ROOT / 'Assets/GearCraft/ScriptableObject/UpgradeParts'
MATERIAL = ['Scrap', 'Gear', 'UpgradeCore', 'ModuleCore_lv1', 'ModuleCore_lv2', 'ModuleCore_lv3']
RARITY = ['Common', 'Uncommon', 'Rare', 'Epic']
EFFECTS = {0: 'DamageFlat', 1: 'AttackSpeedMult', 2: 'BulletDouble', 3: 'SpreadReduction',
           4: 'SpreadIncrease', 6: 'Ricochet', 7: 'JunkCollector', 8: 'BulletSizeUp', 10: 'MagnetRangeUp'}


def scalar(text, field):
    return re.search(r'^  '+re.escape(field)+r': (.*)$', text, re.M).group(1)


def rows(text, field):
    block = re.search(
        r'^  '+field+r':\n((?:(?:  - |    ).*\n)*)', text, re.M).group(1)
    return [dict(re.findall(r'(?:^|\n)  (?:- |  )(\w+): (.*)', row))
            for row in re.split(r'(?=^  - )', block, flags=re.M) if row.strip()]


def load():
    result = {}
    for path in sorted(PARTS.glob('*.asset')):
        text = path.read_text()
        w, h = int(scalar(text, 'width')), int(scalar(text, 'height'))
        shape = [int(x) for x in bytes.fromhex(scalar(text, 'shapeData'))]
        assert len(shape) == w*h
        guid = re.search(r'^guid: (\w+)', Path(str(path)+'.meta').read_text(), re.M).group(1)
        result[path.stem] = dict(asset=path.stem, name=scalar(text, 'partName'), guid=guid,
            rarity=RARITY[int(scalar(text, 'rarity'))], width=w, height=h,
            shape=[[bool(shape[y*w+x]) for x in range(w)] for y in range(h)],
            cells=sum(shape), costs={MATERIAL[int(c['type'])]: int(c['amount']) for c in rows(text, 'costs')},
            effects=[dict(type=int(e['type']), name=EFFECTS[int(e['type'])], value=float(e['value'])) for e in rows(text, 'effects')],
            ranged_only=scalar(text, 'rangedOnly')=='1', sha256=hashlib.sha256(text.encode()).hexdigest())
    scene = (ROOT/'Assets/GearCraft/Scenes/Craft.unity').read_text()
    block = scene.split('  allParts:\n')[1].split('  shopSlotCount:')[0]
    guids = re.findall(r'guid: (\w+)', block)
    stock = [next(k for k,v in result.items() if v['guid']==guid) for guid in guids]
    for part in result.values():
        part['shop_registered'] = part['asset'] in stock
        part['earliest_shop_stage'] = ('No normal acquisition reference found' if not part['shop_registered']
            else 'Day5 workbench after Day4 lv2 drops; random core/stock required' if part['costs'].get('UpgradeCore',0)
            else 'No boss/day unlock; materials and random stock required')
    return result, stock


def pack(parts, names, size):
    # Mirrors GetRotatedShape and CanPlace: only true cells occupy/check the board.
    ordered = sorted(enumerate(names), key=lambda x: -parts[x[1]]['cells'])
    def variants(shape):
        seen=set()
        for rotation in range(4):
            key=tuple(tuple(row) for row in shape)
            if key not in seen:
                seen.add(key);yield rotation,[(x,y) for y,row in enumerate(shape) for x,v in enumerate(row) if v]
            shape=[list(row) for row in zip(*shape[::-1])]
    def recurse(i, occupied, placements):
        if i==len(ordered): return placements
        index,name=ordered[i]
        for rotation,cells in variants(parts[name]['shape']):
            for y in range(size):
                for x in range(size):
                    current={(x+cx,y+cy) for cx,cy in cells}
                    if any(cx>=size or cy>=size for cx,cy in current) or current & occupied: continue
                    found=recurse(i+1,occupied|current,placements+[dict(asset=name,gridX=x,gridY=y,rotation=rotation)])
                    if found is not None:return found
        return None
    return recurse(0,set(),[])


def shop_roll(rng, stock, high):
    first=rng.choice(high)
    return [first]+rng.sample([n for n in stock if n!=first],5)


def shop_feasibility(parts, stock, names, budget, runs=5000):
    costs=collections.Counter()
    for name in names:costs.update(parts[name]['costs'])
    assert all(costs[m]<=budget.get(m,0) for m in costs)
    max_rolls=1+budget['Gear']-costs['Gear']
    high=[n for n in stock if parts[n]['rarity'] in ['Rare','Epic']]
    success=0;witness=None;roll_counts=[]
    for seed in range(runs):
        rng=random.Random(20261015+seed);wanted=collections.Counter(names);trace=[]
        for roll in range(max_rolls):
            lineup=shop_roll(rng,stock,high)
            bought=[]
            for name in lineup:
                if wanted[name]>0:wanted[name]-=1;bought.append(name)
            trace.append(dict(lineup=lineup,bought=bought))
            if not any(wanted.values()):
                success+=1;roll_counts.append(roll)
                if witness is None:witness=dict(seed=20261015+seed,rerolls=roll,total_costs=dict(costs+collections.Counter(Gear=roll)),trace=trace)
                break
    return dict(runs=runs,max_paid_rerolls=max_rolls-1,success_fraction=success/runs,
                mean_paid_rerolls_if_completed=sum(roll_counts)/len(roll_counts) if roll_counts else None,
                witness=witness,assumption='One free daily lineup, then Gear1 per refresh; no tutorial guarantees or prior daily stock; greedily buy only needed pieces. Each asset once per lineup; duplicates require subsequent lineups.')


def summarize_effects(parts,names):
    d=dict(flat_damage=0,attack_interval_multiplier=1,bullet_double=False,spread_modifier=0,
           ricochet=0,bullet_size_multiplier=1,junk_multiplier=1,magnet_range=3)
    for name in names:
        for effect in parts[name]['effects']:
            kind,v=effect['type'],effect['value']
            if kind==0:d['flat_damage']+=v
            elif kind==1:d['attack_interval_multiplier']*=v
            elif kind==2:d['bullet_double']=True
            elif kind==3:d['spread_modifier']-=v
            elif kind==4:d['spread_modifier']+=v
            elif kind==6:d['ricochet']+=int(v)
            elif kind==7:d['junk_multiplier']+=v
            elif kind==8:d['bullet_size_multiplier']+=v
            elif kind==10:d['magnet_range']+=v
    return d


def main():
    parts,stock=load(); builds=[]
    candidates=[
      ('tutorial_day2',3,dict(Scrap=9,Gear=0,UpgradeCore=0),['Damage+1','Damage+2']),
      ('early_day3_common',3,dict(Scrap=24,Gear=6,UpgradeCore=0),['Damage+2']*2+['SpeedGear+','SpeedGear']),
      ('mid_day4_common',4,dict(Scrap=48,Gear=12,UpgradeCore=0),['Damage+2']*3+['SpeedGear+']*2+['SpeedGear']*2),
      ('mid_day4_precision',4,dict(Scrap=48,Gear=12,UpgradeCore=0),['Damage+2']*3+['SpeedGear+']*2+['SpeedGear']*2+['PrecisionNozzle']),
      ('mid_day4_size',4,dict(Scrap=48,Gear=12,UpgradeCore=0),['Damage+2']*2+['SpeedGear+']*2+['SpeedGear']*2+['HeavySlug']),
      ('late_day7_common',5,dict(Scrap=72,Gear=24,UpgradeCore=4),['Damage+2']*4+['SpeedGear++']*3+['SpeedGear+','SpeedGear']),
      ('late_day7_ranged',5,dict(Scrap=72,Gear=24,UpgradeCore=4),['Damage+2']*4+['SpeedGear++']*3+['SpeedGear+','SpeedGear','TwinBarrel','HeavySlug']),
      ('early_day3_speed',3,dict(Scrap=24,Gear=6,UpgradeCore=0),['SpeedGear+']*3+['SpeedGear']),
      ('mid_day4_speed',4,dict(Scrap=48,Gear=12,UpgradeCore=0),['SpeedGear+']*4+['SpeedGear']*4),
      ('late_day7_melee',5,dict(Scrap=72,Gear=24,UpgradeCore=4),['Damage+2']*4+['SpeedGear++']*3+['SpeedGear+']+['SpeedGear']*4),
      ('late_day7_precision',5,dict(Scrap=72,Gear=24,UpgradeCore=4),['Damage+2']*4+['SpeedGear++']*3+['SpeedGear+','SpeedGear','TwinBarrel']+['PrecisionNozzle']*2),
      ('late_day7_reflect',5,dict(Scrap=72,Gear=24,UpgradeCore=4),['Damage+2']*4+['SpeedGear++']*2+['SpeedGear+']*2+['SpeedGear','TwinBarrel','ReflectionBullet','PrecisionNozzle']),
      ('final_day11_melee',6,dict(Scrap=96,Gear=30,UpgradeCore=8),['Damage+2']*4+['SpeedGear++']*6+['SpeedGear+']*2+['SpeedGear']*3),
      ('final_day11_ranged',6,dict(Scrap=96,Gear=30,UpgradeCore=8),['Damage+2']*4+['SpeedGear++']*5+['SpeedGear+']*2+['SpeedGear']*3+['TwinBarrel']+['PrecisionNozzle']*2)]
    for name,size,budget,names in candidates:
        assert all(n in stock for n in names)
        placements=pack(parts,names,size);assert placements is not None,name
        costs=collections.Counter()
        for n in names:costs.update(parts[n]['costs'])
        shop_result=({'tutorial_guaranteed':True,'assumption':'First-time WeaponCustom supplies missing Scrap3 then Scrap6 and inserts both assets; completion is required.'}
                     if name=='tutorial_day2' else shop_feasibility(parts,stock,names,budget))
        builds.append(dict(name=name,grid_size=size,budget=budget,parts=names,part_costs=dict(costs),
            occupied_cells=sum(parts[n]['cells'] for n in names),placements=placements,
            effects=summarize_effects(parts,names),shop=shop_result))
    # Reuse the prior economic simulation without writing/altering its files or __pycache__.
    previous=ROOT/'QA/WeaponBalance20261015/economic_model.py'
    scope={'__file__':str(previous),'__name__':'economic_model_read_only'}
    exec(compile(previous.read_text(),str(previous),'exec'),scope)
    config,pools,cards,recipes=scope['load_inputs']()
    economic=[]
    for tutorial_day in [1,2]:
        rng=random.Random(20261015+tutorial_day);trials=[scope['simulate'](rng,config,pools,cards,recipes,tutorial_day) for _ in range(3000)]
        stages=[]
        for day,scrap,gear,core,weapon_gear in [(2,9,0,0,10),(3,24,6,0,10),(4,48,12,0,40),(5,48,12,2,40),(7,72,24,4,70),(11,72,24,4,70),(11,96,30,8,70)]:
            values=[trial[1][day-2]['inventory'] for trial in trials]
            required=dict(Scrap=scrap,Gear=gear+weapon_gear,UpgradeCore=core)
            enough=lambda v,extra:all(v[m]>=c+(extra if m=='UpgradeCore' else 0) for m,c in required.items())
            stages.append(dict(before_day=day,part_budget=dict(Scrap=scrap,Gear=gear,UpgradeCore=core),
                reserved_weapon_gear=weapon_gear,mean_unspent={m:sum(v[m] for v in values)/len(values) for m in ['Scrap','Gear','UpgradeCore']},
                fraction_resources_sufficient=sum(enough(v,0) for v in values)/len(values),
                fraction_with_additional_four_upgrade_modules=sum(enough(v,12) for v in values)/len(values),
                limitation='Resource-only test, no AR ownership condition, no shop RNG, no survival; old model still assumes Katana and economic reward selection. Weapon Gear reserve covers GearCraft then one mid weapon then one late weapon, not every weapon. Module20 reserve here checks UpgradeCore12 only; ModuleCore_lv2 also required.'))
        economic.append(dict(tutorial_before_day=tutorial_day,runs=3000,stages=stages))
    high=[n for n in stock if parts[n]['rarity'] in ['Rare','Epic']]
    output=dict(kind='Static audit and offline feasibility models; not gameplay measurements',catalog=list(parts.values()),
       shop=dict(registered=stock,slots=6,paid_refresh_gear=1,guaranteed_high_rarity_count=len(high),
          per_lineup_probability_low=5/(len(stock)-1),
          per_lineup_probability_high=1/len(high)+(1-1/len(high))*5/(len(stock)-1)),
       builds=builds,economy=economic,prior_model_sha256=hashlib.sha256(previous.read_bytes()).hexdigest())
    (OUT/'upgrade-parts-audit.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
    print('Catalog:',len(parts),'shop:',len(stock),'high-rarity:',len(high))
    for b in builds:print(b['name'],b['part_costs'],b['effects'],'shop success',round(b['shop'].get('success_fraction',1),4))
    for e in economic:print('Tutorial',e['tutorial_before_day'],[(r['before_day'],round(r['fraction_resources_sufficient'],3),round(r['fraction_with_additional_four_upgrade_modules'],3)) for r in e['stages']])


if __name__=='__main__':main()
