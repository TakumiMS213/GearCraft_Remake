#!/usr/bin/env python3
"""Read native result CSVs; write only this continuation's audit artifacts."""
from pathlib import Path
from decimal import Decimal
from collections import Counter
import csv
import hashlib
import json

HERE = Path(__file__).resolve().parent
OLD = HERE.parent / 'ReleaseFinalization20261018'
NEW_LABEL = 'gate_continuation_EK9_SteamThrower_velocityfixed_seed77_fps60'
OLD_LABEL = 'gate_native_ranged_EK9_SteamThrower_seed77_fps60'
OLD_FIXED = 'gate_native_ranged_EK9_SteamThrower_velocityfixed_seed77_fps60'
FIELDS = ['STR', 'ACC', 'bonusDamage', 'craftWeaponDamagebuff', 'attackSpeedMult', 'hasBulletDouble',
          'spreadModifier', 'ricochetCount', 'bulletSizeMult', 'punkDrive', 'module_repair', 'module_barrier', 'module_scrap']


def csvrows(path):
    with path.open(newline='') as f:
        return list(csv.DictReader(f))


def read_case(folder, label):
    result = dict(line.split('=', 1) for line in (folder / (label + '-result.txt')).read_text().splitlines() if '=' in line)
    bosses = csvrows(folder / (label + '-bosses.csv'))
    spawned = next(r for r in bosses if r['event'] == 'spawned')
    ended = next(r for r in bosses if r['event'] == 'ended')
    start, end = float(spawned['spawn_game_seconds']), float(ended['event_game_seconds'])
    inputs = csvrows(folder / (label + '-native-input.csv'))
    events = csvrows(folder / (label + '-events.csv'))
    geo = csvrows(folder / (label + '-geometry.csv'))
    status = json.loads((folder / (label + '-initial-status.json')).read_text())
    counts = Counter(r['event'] for r in events if start <= float(r['game_seconds']) <= end)
    intercepts = [r for r in events if start <= float(r['game_seconds']) <= end and r['event'] == 'reactive_intercept_target']
    checkpoints = {}
    for offset in (1.75, 30.0, 57.9):
        if end - start < offset:
            continue
        r = min(geo, key=lambda row: abs(float(row['game_seconds']) - start - offset))
        checkpoints[str(offset)] = dict(boss_relative_seconds=float(r['game_seconds']) - start,
            boss_hp=r['boss_hp'], player_x=r['player_x'], boss_x=r['boss_x'],
            boss_attack_events=int(r['attack_events']) - int(geo[0]['attack_events']))
    checks = dict(
        unique_input_frames=len(inputs) == len({r['frame'] for r in inputs}),
        attack_total_matches=sum(int(r['accepted_attacks']) for r in inputs) == int(result['attack_events']),
        expected_projectile_total_matches=sum(int(r['expected_projectiles']) for r in inputs) == int(result['expected_projectiles']),
        double_expected_per_input=all(int(r['expected_projectiles']) == 2 * int(r['accepted_attacks']) for r in inputs),
        no_false_false_attacks=all(int(r['accepted_attacks']) == 0 for r in inputs if r['pressed'] == r['held'] == 'False'),
        hp_balance=Decimal(result['start_hp']) - Decimal(result['player_damage']) + Decimal(result['player_healed']) == Decimal(result['end_hp']),
        gate_balance=Decimal(result['start_gate']) - Decimal(result['gate_damage']) + Decimal(result['gate_healed']) == Decimal(result['end_gate']),
        boss_hp_balance=Decimal(ended['start_hp']) - Decimal(ended['player_damage']) + Decimal(ended['player_healed']) == Decimal(ended['end_hp']),
    )
    return dict(label=label, result=result, boss_start=spawned, boss_end=ended, initial_combat_status={k: status[k] for k in FIELDS},
        input_rows=len(inputs), checks=checks, boss_event_counts=dict(counts),
        boost_uses=sum(r['event'] == 'reactive_boost' for r in events),
        boss_intercept_selected=sum(r['details'] != 'return_to_enemy' for r in intercepts),
        boss_intercept_return=sum(r['details'] == 'return_to_enemy' for r in intercepts),
        boss_request_counts=dict(Counter(r['request_reason'] for r in inputs if start <= float(r['game_seconds']) <= end)),
        checkpoints=checkpoints,
        min_player_y=min(float(r['player_y']) for r in geo),
        max_player_floor_depth=max(float(r['player_floor_depth']) for r in geo),
        overlap_frames=sum(float(r['player_boss_overlap_x']) > 0 and float(r['player_boss_overlap_y']) > 0 for r in geo),
        source_sha256={suffix: hashlib.sha256((folder / (label + suffix)).read_bytes()).hexdigest()
            for suffix in ['-result.txt', '-bosses.csv', '-native-input.csv', '-events.csv', '-geometry.csv', '-initial-status.json']})


current = read_case(HERE, NEW_LABEL)
previous = read_case(OLD, OLD_LABEL)
previous_fixed = read_case(OLD, OLD_FIXED)
assert all(current['checks'].values()), current['checks']
assert current['initial_combat_status'] == previous['initial_combat_status'] == previous_fixed['initial_combat_status']
assert current['result']['result'] == 'ending_reached_3' and current['boss_end']['result'] == 'defeated'
assert (HERE / 'batch-progress.txt').read_text().startswith('COMPLETE all_cases_finished completed=1/1')
audit = dict(current=current, previous_invalid=previous, previous_fixed_invalid=previous_fixed,
    same_initial_combat_fields=True,
    screen={'mcp_editor_context_screen_display_observed': '1672x924',
            'current_selected_size_label': 'FullHD/index3', 'current_resize_requested': False,
            'post_run_title_root_observed_gameview_target_render_size': '1920x1080',
            'post_run_title_root_observed_camera_pixels': '1920x1080',
            'post_run_title_root_observed_canvas_pixel_rect': '1920x1080',
            'battle_camera_dimensions_recorded': False, 'previous_battle_camera_dimensions_recorded': False,
            'limitation': 'Screen/Display in MCP Editor context differed from simultaneous Title GameView/Camera/Canvas dimensions. Do not identify 1672x924 as battle render size or infer unequal resolutions. Equivalent rendering conditions between combat attempts remain unverified; prior QA480 setting history is not proof of actual battle dimensions.'},
    scope='One injected final-day native bot run; no acquisition playthrough, human success rate, new balance adjustment, or extrapolated old kill time.')
(HERE / 'combat-result-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2) + '\n')
print('PASS: input/event/HP/gate reconciliation; three initial combat configurations match; new defeat and Ending3 confirmed.')
