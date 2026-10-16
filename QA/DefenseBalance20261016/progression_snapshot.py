#!/usr/bin/env python3
"""Reproduce the conditional resource medians, without touching Unity or game state."""
import importlib.util, random, statistics, json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
model = root / "QA/WeaponBalance20261015/economic_model.py"
spec = importlib.util.spec_from_file_location("economy", model)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
config, pools, cards, recipes = m.load_inputs()
rng = random.Random(20261016)
rows = [m.simulate(rng, config, pools, cards, recipes, 1)[1] for _ in range(1000)]
result = dict(kind="Offline median resource model, not player telemetry or survival measurement", runs=1000, seed=20261016,
 model=str(model.relative_to(root)), assumptions="Tutorial before Day1; all scheduled kills/drops; Katana reward pool AR then material priority; ScrapModule+Punk purchased; other purchases not yet subtracted; no summons/junk/weapon-specific cards",
 config=config, recipes=recipes, before_day=[], source_sha256=m.SOURCES)
for day in (3, 6, 10, 11):
 records = [row[day-2] for row in rows]
 result["before_day"].append(dict(day=day, stage_start=(day-1)*3+1,
  median_unspent={key: statistics.median(row["inventory"][key] for row in records) for key in m.MATERIALS},
  mean_unspent={key: statistics.mean(row["inventory"][key] for row in records) for key in m.MATERIALS},
  ar_owned_fraction=statistics.mean(row["ar_owned"] for row in records)))
Path(__file__).with_name("progression-economic-snapshot.json").write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n")
