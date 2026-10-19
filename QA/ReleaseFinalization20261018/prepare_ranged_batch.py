#!/usr/bin/env python3
"""Build a separate EK ranged plan/batch from the frozen native-input batch."""
from pathlib import Path
import csv
import hashlib
import io
import json

root = Path(__file__).resolve().parent
source_plan = root.parent / "DefenseBalance20261016" / "batch-cases.csv"
source_batch = root / "BatchNativeInput.cs.txt"
original_plan = root / "native-input-cases.csv"
destination = root / "native-ranged-cases.csv"
fields = list(next(csv.DictReader(original_plan.open())))
lookup = {r["weapon"]: r for r in csv.DictReader(source_plan.open())
          if r["start_stage"] == "31" and r["seed"] == "77"}
rows = []
for weapon in ("RailCraft", "SteamGatling", "SteamCannon", "SteamThrower"):
    row = dict(lookup[weapon])
    row["label"] = "gate_native_ranged_EK9_" + weapon + "_seed77_fps60"
    row["fps"] = "60"
    assert row["moduleDamage"] == "20" and row["STR"] == row["ACC"] == "20"
    rows.append(row)
buffer = io.StringIO(newline="")
writer = csv.DictWriter(buffer, fieldnames=fields, lineterminator="\n")
writer.writeheader(); writer.writerows(rows)
plan = buffer.getvalue()
if destination.exists() and destination.read_text() != plan:
    raise ValueError("Changed ranged plan exists; review instead of overwriting")
destination.write_text(plan)
digest = lambda b: hashlib.sha256(b).hexdigest()
source = source_batch.read_text()
assert source.count(original_plan.name) == 1
old_hash = digest(original_plan.read_bytes())
assert source.count(old_hash) == 1
output = source.replace(original_plan.name, destination.name).replace(old_hash, digest(destination.read_bytes()))
(root / "BatchNativeRanged.cs.txt").write_text(output)
loader = (root / "LoadBatchNativeInput.cs.txt").read_text()
(root / "LoadBatchNativeRanged.cs.txt").write_text(loader.replace("BatchNativeInput.cs.txt", "BatchNativeRanged.cs.txt"))
manifest = dict(source_batch_sha256=digest(source_batch.read_bytes()),
                source_plan_sha256=digest(source_plan.read_bytes()),
                new_plan_sha256=digest(destination.read_bytes()),
                output_sha256=digest(output.encode()), case_count=4,
                changes=["Plan filename and plan SHA only; native route/bootstrap unchanged"],
                runtime_verification="pending root execution")
(root / "native-ranged-build-manifest.json").write_text(json.dumps(manifest, indent=2)+"\n")
assert source_batch.read_text() == source
print("Prepared separate 4-case ranged batch; original batch and original plan unchanged")
