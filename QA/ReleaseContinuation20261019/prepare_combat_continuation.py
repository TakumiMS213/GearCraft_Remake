#!/usr/bin/env python3
"""Prepare one native SteamThrower EK retry. QA source only; never copy backup/results."""
from pathlib import Path
import csv
import hashlib
import io
import json

HERE = Path(__file__).resolve().parent
OLD = HERE.parent / 'ReleaseFinalization20261018'
OLD_FOLDER = 'QA/ReleaseFinalization20261018/'
NEW_FOLDER = 'QA/ReleaseContinuation20261019/'
OLD_SESSION = 'GearCraft.QA.ReleaseFinalization.Session'
NEW_SESSION = 'GearCraft.QA.ReleaseContinuation.Session'
LABEL = 'gate_continuation_EK9_SteamThrower_velocityfixed_seed77_fps60'
PLAN = 'native-thrower-velocityfixed-cases.csv'
BOOT = 'NativeInputBootstrap.cs.txt'
ROUTE = 'NativeInputSurvivalRoute.cs.txt'
BATCH = 'BatchNativeThrowerVelocityFixed.cs.txt'
LOADER = 'LoadBatchNativeThrowerVelocityFixed.cs.txt'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def once(text, before, after):
    assert text.count(before) == 1, (before, text.count(before))
    return text.replace(before, after, 1)


def write_reviewed(name, data):
    target = HERE / name
    if target.exists() and target.read_bytes() != data:
        raise RuntimeError('Existing reviewed file differs; do not overwrite: ' + name)
    if not target.exists():
        target.write_bytes(data)


def relocate(text):
    return text.replace(OLD_FOLDER, NEW_FOLDER).replace(OLD_SESSION, NEW_SESSION)


def route_deadline(text):
    return once(text, 'if(now-wallStarted>=300){finish("wall_timeout_300s");return;}',
                'if(now-wallStarted>=140){finish("wall_timeout_140s");return;}')


def main():
    names = (PLAN, BOOT, ROUTE, BATCH, LOADER, 'loadouts.csv', 'loadout-placements.csv')
    original = {n: (OLD / n).read_bytes() for n in names}
    reader = csv.DictReader(io.StringIO(original[PLAN].decode()))
    fields = reader.fieldnames
    rows = list(reader)
    assert len(rows) == 1
    old_row = rows[0]
    assert old_row['weapon'] == 'SteamThrower' and old_row['start_stage'] == '31'
    assert old_row['seed'] == '77' and old_row['fps'] == '60'
    assert old_row['STR'] == old_row['ACC'] == old_row['moduleDamage'] == '20'
    assert old_row['profile'] == 'final_day11_thrower_fitted_damage_defense_module20'
    new_row = dict(old_row, label=LABEL)
    assert {k: v for k, v in new_row.items() if k != 'label'} == {k: v for k, v in old_row.items() if k != 'label'}
    buffer = io.StringIO(newline='')
    writer = csv.DictWriter(buffer, fieldnames=fields, lineterminator='\n')
    writer.writeheader(); writer.writerow(new_row)
    plan = buffer.getvalue().encode()
    bootstrap = relocate(original[BOOT].decode())
    route = route_deadline(relocate(original[ROUTE].decode()))
    batch = route_deadline(relocate(original[BATCH].decode()))
    batch = once(batch, 'if(bNow-bStartedAt>60+bPending.Count*380){bStop("batch_wall_timeout",true);return;}',
                 'if(bNow-bStartedAt>150){bStop("batch_wall_timeout_150s",true);return;}')
    batch = once(batch, 'if(bNow-bPhaseAt>340){bStop("route_wall_watchdog",true);return;}',
                 'if(bNow-bPhaseAt>145){bStop("route_wall_watchdog_145s",true);return;}')
    batch = once(batch, sha(original[BOOT]), sha(bootstrap.encode()))
    batch = once(batch, sha(original[ROUTE]), sha(route.encode()))
    batch = once(batch, sha(original[PLAN]), sha(plan))

    # Confirm the generated body embeds exactly the reviewed bootstrap/route transformations.
    embedded_bootstrap = once(bootstrap,
        'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;',
        'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;\nbBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};')
    embedded_route = once(route, 'UnityEditor.EditorApplication.isPlaying=false;', 'Time.timeScale=0f; // Batch retains Play between cases.')
    embedded_route = once(embedded_route, 'System.Func<BonusCardDataSO,float> cardScore=data=>{', 'bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{')
    assert batch.count(embedded_bootstrap) == 1
    assert batch.count(embedded_route) == 1
    assert 'DG.Tweening.DOTween.logBehaviour=DG.Tweening.LogBehaviour.Default;' in batch
    loader = relocate(original[LOADER].decode())
    outputs = {PLAN: plan, BOOT: bootstrap.encode(), ROUTE: route.encode(), BATCH: batch.encode(), LOADER: loader.encode(),
               'loadouts.csv': original['loadouts.csv'], 'loadout-placements.csv': original['loadout-placements.csv']}
    for name, data in outputs.items():
        if name.endswith('.cs.txt'):
            assert OLD_FOLDER.encode() not in data and OLD_SESSION.encode() not in data
        write_reviewed(name, data)
    manifest = {
        'kind': 'one-native-thrower-ek-continuation', 'label': LABEL, 'case_count': 1,
        'source_folder': OLD_FOLDER, 'output_folder': NEW_FOLDER,
        'source_read_only_sha256': {n: sha(b) for n, b in original.items()},
        'outputs_sha256': {n: sha(b) for n, b in outputs.items()},
        'configuration_changes': ['label only'],
        'runner_changes': ['QA folder', 'fresh SessionState key', 'plan/bootstrap/route hash guards', 'route wall deadline 300 -> 140 seconds', 'batch route watchdog 340 -> 145 seconds', 'whole batch wall deadline -> 150 seconds'],
        'native_attack_bot_physics_changes': [],
        'source_files_unchanged': all((OLD / n).read_bytes() == b for n, b in original.items()),
        'backup_or_result_files_copied': False,
        'deadline_limitation': 'Editor update watchdog; a blocked Editor main thread cannot execute cancellation until it resumes. Not an OS-enforced timeout.',
        'runtime_verification': 'pending root execution',
    }
    write_reviewed('combat-continuation-manifest.json', (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode())
    print('Prepared one unchanged build / native bot case in new QA; fresh backup required; Editor watchdog 150s.')


if __name__ == '__main__':
    main()
