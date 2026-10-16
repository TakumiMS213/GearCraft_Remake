"""Merge valid native combat records, keeping discarded pilot runs auditable."""
import csv
from pathlib import Path
P=Path(__file__).resolve().parent
late={'RailCraft','SteamGatling'}
source=P/'route-before-aim-results.csv'
base=list(csv.DictReader(source.open()))
valid={r['id'] for r in base if r['weapon'] not in late}
order=[r.split(',')[0] for r in (P/'route-cases.csv').read_text().splitlines() if r and not r.startswith('#')]
origins={}
for suffix,key in [('results.csv','id'),('spawn-plan.csv','case'),('scheduled-spawns.csv','case'),('spawns.csv','case')]:
    first=P/f'route-before-aim-{suffix}'
    second=P/f'route-resume-{suffix}'
    with first.open() as f:
        reader=csv.DictReader(f); fields=reader.fieldnames; rows=[r for r in reader if r[key] in valid]
    for r in rows: origins[r[key]]='route-before-aim (muzzle Y offset = 0 or melee; unaffected by correction)'
    if second.exists():
        with second.open() as f: new=list(csv.DictReader(f))
        # Ignore an incomplete last write from a live benchmark.
        new=[r for r in new if all(r.get(x) is not None for x in fields)]
        rows.extend(new)
        for r in new: origins[r[key]]='route-resume (corrected lateral muzzle aiming)'
    rows.sort(key=lambda r:order.index(r[key]) if r[key] in order else 999)
    with (P/f'route-{suffix}').open('w') as f:
        writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(rows)
with (P/'route-provenance.csv').open('w') as f:
    w=csv.writer(f);w.writerow(['case','raw_source']);w.writerows((k,origins[k]) for k in order if k in origins)
print('Merged valid completed cases:',sum(1 for _ in csv.DictReader((P/'route-results.csv').open())))
