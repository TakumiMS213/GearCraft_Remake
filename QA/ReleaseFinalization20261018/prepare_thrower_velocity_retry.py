#!/usr/bin/env python3
"""Separate retry; preserve the failed pre-fix trial and both previous plans."""
from pathlib import Path
import csv
import hashlib
import io
import json

p = Path(__file__).resolve().parent
source = p / 'BatchNativeRanged.cs.txt'
old_plan = p / 'native-ranged-cases.csv'
new_plan = p / 'native-thrower-velocityfixed-cases.csv'
digest = lambda b: hashlib.sha256(b).hexdigest()
source_bytes, plan_bytes = source.read_bytes(), old_plan.read_bytes()
reader = csv.DictReader(io.StringIO(plan_bytes.decode()))
fields = reader.fieldnames
rows = [r for r in reader if r['weapon'] == 'SteamThrower']
assert len(rows) == 1
row = rows[0]
old_label = row['label']
row['label'] = 'gate_native_ranged_EK9_SteamThrower_velocityfixed_seed77_fps60'
assert row['start_stage'] == '31' and row['seed'] == '77' and row['fps'] == '60'
assert row['STR'] == row['ACC'] == row['moduleDamage'] == '20'
buffer = io.StringIO(newline='')
writer = csv.DictWriter(buffer, fieldnames=fields, lineterminator='\n')
writer.writeheader(); writer.writerow(row)
plan_text = buffer.getvalue()
if new_plan.exists() and new_plan.read_text() != plan_text:
    raise ValueError('Retry plan already differs; do not overwrite reviewed input')
new_plan.write_text(plan_text)
body = source_bytes.decode()
assert body.count(old_plan.name) == 1 and body.count(digest(plan_bytes)) == 1
body = body.replace(old_plan.name, new_plan.name).replace(digest(plan_bytes), digest(new_plan.read_bytes()))
guard = 'new[]{"batch-progress.txt","native-survival-progress.txt","benchmark-progress.txt"}'
assert body.count(guard) == 1
body = body.replace(guard, 'new[]{"batch-progress.txt","native-survival-progress.txt","benchmark-progress.txt","ui-visual-progress.txt"}')
marker = 'Debug.unityLogger.filterLogType=LogType.Warning;'
assert body.count(marker) == 1
body = body.replace(marker, '''// Retry after the three serialized ParticleSystem velocity-mode corrections.
// Keep detailed DOTween target warnings active through teardown. Canonical restore owns final settings.
System.IO.File.AppendAllText(bFolder+"batch-runtime-before-"+bSession+".txt","dotweenLogBehaviour="+DG.Tweening.DOTween.logBehaviour+"\\n");
DG.Tweening.DOTween.logBehaviour=DG.Tweening.LogBehaviour.Default;
Debug.unityLogger.logEnabled=true;
Debug.unityLogger.filterLogType=LogType.Warning;''')
destination = p / 'BatchNativeThrowerVelocityFixed.cs.txt'
destination.write_text(body)
loader = (p / 'LoadBatchNativeRanged.cs.txt').read_text().replace(source.name, destination.name)
(p / 'LoadBatchNativeThrowerVelocityFixed.cs.txt').write_text(loader)
manifest = {
    'source_batch': source.name, 'source_batch_sha256': digest(source_bytes),
    'source_plan': old_plan.name, 'source_plan_sha256': digest(plan_bytes),
    'source_label': old_label, 'new_label': row['label'], 'case_count': 1,
    'new_plan_sha256': digest(new_plan.read_bytes()), 'output_sha256': digest(destination.read_bytes()),
    'configuration_changes': [],
    'runner_changes': ['Plan filename/hash', 'UI-progress overlap guard', 'DOTween Default and logger enabled for detailed missing-target warnings'],
    'previous_timeout_preserved': (p / (old_label + '-result.txt')).exists(),
    'runtime_verification': 'pending root execution',
}
(p / 'native-thrower-velocityfixed-build-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
assert source.read_bytes() == source_bytes and old_plan.read_bytes() == plan_bytes
print('Prepared one velocity-fixed Thrower EK retry. Prior batch, plan and results unchanged.')
