#!/usr/bin/env python3
"""QA-only single -force-gfx-direct comparison. Prepared; never run without --execute.

No Swift compilation, UI/input/screen APIs, or permission changes. Normal quit is
the test; TERM/KILL of this runner's Popen child is cleanup, never a passing quit.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time

from standalone_smoke import (
    QA, APP, PLAYER_DOMAIN, PROTECTED_DOMAINS, PREFS, utc, digest, canonical,
    write_new, dump_new, progress, app_identity, support_manifest, file_changes,
    console_findings,
)

PRIOR = QA / 'standalone-smoke-run1'
HELPER = PRIOR / 'normal-quit-helper'


class Budget:
    def __init__(self):
        self.start = time.monotonic()
        self.deadline = self.start + 59.0  # Leave one second for final local records.

    def remaining(self):
        return self.deadline - time.monotonic()

    def run(self, args, timeout=1.5):
        available = self.remaining()
        if available < 0.1:
            raise TimeoutError('Overall 60-second budget exhausted')
        return subprocess.run(args, capture_output=True, timeout=min(timeout, available))


def snapshot(domain, budget):
    path = PREFS / (domain + '.plist')
    existed = path.exists()
    raw = path.read_bytes() if existed else None
    reply = budget.run(['/usr/bin/defaults', 'export', domain, '-'])
    if reply.returncode:
        raise RuntimeError('defaults export failed: ' + domain)
    values = plistlib.loads(reply.stdout)
    if not isinstance(values, dict):
        raise RuntimeError('Preference domain is not a dictionary')
    meta = {
        'domain': domain, 'exists': existed,
        'raw_sha256': None if raw is None else digest(raw),
        'typed_sha256': digest(canonical(values)), 'key_count': len(values),
        'disk_matches_export': None if raw is None else canonical(plistlib.loads(raw)) == canonical(values),
    }
    return meta, raw, reply.stdout, values


def record_snapshot(run, prefix, snap):
    meta, raw, exported, _ = snap
    dump_new(run / (prefix + '.json'), meta)
    write_new(run / (prefix + '.export.plist'), exported)
    if raw is not None:
        write_new(run / (prefix + '.raw.plist'), raw)


def protected(budget):
    return {domain: snapshot(domain, budget)[0] for domain in PROTECTED_DOMAINS}


def player_processes(budget, executable):
    reply = budget.run(['/bin/ps', '-axww', '-o', 'pid=,comm='])
    if reply.returncode:
        raise RuntimeError('Cannot verify single Player instance; no launch/restore allowed')
    matches = []
    for line in reply.stdout.decode(errors='replace').splitlines():
        row = line.strip().split(None, 1)
        if len(row) == 2 and Path(row[1]).name == Path(executable).name:
            matches.append({'pid': int(row[0]), 'executable': row[1]})
    return matches


def restore_player(run, budget, before, protected_before, identity, state):
    # Called only after the owned child has been reaped and no same-name Player remains.
    after = snapshot(PLAYER_DOMAIN, budget)
    record_snapshot(run, 'player-domain-after', after)
    after_values = after[3]
    current_protected = protected(budget)
    dump_new(run / 'protected-domains-after.json', current_protected)
    if current_protected != protected_before:
        raise RuntimeError('Protected Editor/compatibility domain changed; manual review required')
    if app_identity() != identity:
        raise RuntimeError('App identity changed; manual review required')
    if after[0]['disk_matches_export'] is not True:
        raise RuntimeError('Player domain disk/cache mismatch after exit')

    # defaults import MERGES. Delete only recorded new keys after verifying the
    # whole merged domain and each key's typed value against the after snapshot.
    before_values = before[3]
    added = sorted(set(after_values) - set(before_values))
    dump_new(run / 'restore-plan.json', {
        'added_key_count': len(added), 'added_keys': added,
        'before_key_count': len(before_values), 'domain': PLAYER_DOMAIN,
        'delete_rule': 'absent before, present in after, current typed value still equals recorded after',
    })
    prewrite = snapshot(PLAYER_DOMAIN, budget)
    if canonical(prewrite[3]) != canonical(after_values) or prewrite[0]['exists'] != after[0]['exists']:
        raise RuntimeError('Player preferences changed after the recorded after snapshot; no writes attempted')
    reply = budget.run(['/usr/bin/defaults', 'import', PLAYER_DOMAIN,
                        str(run / 'player-domain-before.export.plist')])
    write_new(run / 'defaults-import.stderr.txt', reply.stderr)
    if reply.returncode:
        raise RuntimeError('Player defaults import failed')
    state['player_domain_restoration'] = 'imported_verification_pending'
    expected = dict(after_values)
    expected.update(before_values)
    state['removed_added_keys'] = []
    for key in added:
        current = snapshot(PLAYER_DOMAIN, budget)[3]
        if canonical(current) != canonical(expected):
            raise RuntimeError('Concurrent/unexpected preference mutation before limited key deletion')
        if key in before_values or key not in current or canonical(current[key]) != canonical(after_values[key]):
            raise RuntimeError('Added-key deletion guard failed')
        reply = budget.run(['/usr/bin/defaults', 'delete', PLAYER_DOMAIN, key])
        if reply.returncode:
            raise RuntimeError('Limited added-key deletion failed')
        state['removed_added_keys'].append(key)
        del expected[key]
    restored = snapshot(PLAYER_DOMAIN, budget)
    record_snapshot(run, 'player-domain-restored', restored)
    if canonical(restored[3]) != canonical(before_values) or restored[0]['exists'] != before[0]['exists'] or restored[0]['disk_matches_export'] is not True:
        raise RuntimeError('Full Player domain type/value/existence verification failed')
    final_protected = protected(budget)
    dump_new(run / 'protected-domains-final.json', final_protected)
    if final_protected != protected_before:
        raise RuntimeError('Protected domains changed during restoration')
    state['player_domain_restoration'] = 'verified_full_type_value_existence'
    state['restoration_pending'] = False


def probe(run, graphics='direct'):
    budget = Budget()
    run.mkdir(exist_ok=False)
    state = {
        'started_utc': utc(), 'result': 'NOT_STARTED', 'player_pid': None,
        'normal_exit_confirmed': False, 'forced_cleanup': False,
        'process_may_still_be_running': False, 'restoration_pending': False,
        'player_domain_restoration': 'not_needed', 'signals': [],
        'scope': 'One graphics threading comparison; not UI/audio restart validation',
        'requested_graphics': graphics,
    }
    player = stream = before = identity = support_before = None
    protected_before = None
    try:
        prior_cleanup = json.loads((PRIOR / 'cleanup-result.json').read_text())
        if prior_cleanup.get('process_stopped') is not True or prior_cleanup.get('full_player_domain_restored') is not True:
            raise RuntimeError('Prior smoke cleanup/restoration not verified')
        identity = app_identity()
        if identity != json.loads((PRIOR / 'app-before.json').read_text()):
            raise RuntimeError('App/hash differs from run1; this is not the requested one-factor comparison')
        if not HELPER.is_file() or not os.access(HELPER, os.X_OK):
            raise RuntimeError('Existing normal-quit helper unavailable; compilation is forbidden')
        dump_new(run / 'app-before.json', identity)
        dump_new(run / 'helper.json', {'path': str(HELPER), 'sha256': digest(HELPER.read_bytes()), 'reused': True})
        existing = player_processes(budget, identity['executable'])
        dump_new(run / 'single-instance-before.json', {'matches': existing})
        if existing:
            raise RuntimeError('A same-name Player is already running; no launch')
        before = snapshot(PLAYER_DOMAIN, budget)
        record_snapshot(run, 'player-domain-before', before)
        if not before[0]['exists'] or before[0]['disk_matches_export'] is not True:
            raise RuntimeError('Fresh Player domain absent or disk/cache differs; no launch')
        protected_before = protected(budget)
        dump_new(run / 'protected-domains-before.json', protected_before)
        if any(meta['disk_matches_export'] is False for meta in protected_before.values()):
            raise RuntimeError('Protected domain disk/cache mismatch; no launch')
        support_before = support_manifest()
        dump_new(run / 'application-support-before.json', support_before)
        # 5 preparation + 20 observation + 15 normal quit + 10 cleanup + 9 restore.
        if time.monotonic() - budget.start > 5.0:
            raise TimeoutError('Preparation exceeded five seconds; no launch, cleanup budget preserved')
        dump_new(run / 'backup-ready.json', {'utc': utc(), 'fresh_full_domain': True})
        command = [identity['executable'], '-screen-fullscreen', '0', '-screen-width', '1280',
                   '-screen-height', '720', '-logFile', str(run / 'player.log')]
        if graphics == 'direct':
            command.append('-force-gfx-direct')
        stream = (run / 'player-stdio.log').open('xb')
        player = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        state.update(player_pid=player.pid, process_may_still_be_running=True,
                     restoration_pending=True, player_domain_restoration='pending')
        launched = time.monotonic()
        dump_new(run / 'launched-process.json', {'utc': utc(), 'pid': player.pid, 'command': command})
        progress(run, 'player_started', pid=player.pid, graphics=graphics)
        owned = player_processes(budget, identity['executable'])
        dump_new(run / 'single-instance-after-launch.json', {'matches': owned})
        if len(owned) != 1 or owned[0]['pid'] != player.pid or Path(owned[0]['executable']).resolve() != Path(identity['executable']).resolve():
            raise RuntimeError('Owned Player identity/single-instance check failed')
        sampled = False
        next_progress = 5.0
        while time.monotonic() - launched < 20.0:
            if player.poll() is not None:
                raise RuntimeError('Player exited before normal quit request')
            elapsed = time.monotonic() - launched
            if not sampled and elapsed >= 5.0:
                sampled = True
                progress(run, 'sampling_at_five_seconds', pid=player.pid, elapsed_seconds=round(elapsed, 3))
                try:
                    sampled_reply = budget.run(['/usr/bin/sample', str(player.pid), '2', '10',
                                                '-file', str(run / 'process-sample-at5s.txt')], timeout=4.0)
                    write_new(run / 'sample.stdout.txt', sampled_reply.stdout)
                    write_new(run / 'sample.stderr.txt', sampled_reply.stderr)
                    state['sample_exit_code'] = sampled_reply.returncode
                except subprocess.TimeoutExpired:
                    state['sample_timeout'] = True
            if elapsed >= next_progress:
                progress(run, 'observing_player', pid=player.pid, elapsed_seconds=round(time.monotonic() - launched, 3))
                next_progress += 5.0
            time.sleep(0.05)

        progress(run, 'requesting_normal_termination', pid=player.pid)
        request_time = time.monotonic()
        reply = budget.run([str(HELPER), str(player.pid), identity['executable']], timeout=2.0)
        write_new(run / 'normal-quit-helper.stdout.txt', reply.stdout)
        write_new(run / 'normal-quit-helper.stderr.txt', reply.stderr)
        quit_reply = json.loads(reply.stdout)
        state['normal_quit_reply'] = quit_reply
        if reply.returncode or quit_reply.get('accepted') is not True or quit_reply.get('pid') != player.pid:
            raise RuntimeError('Normal quit was not accepted')
        player.wait(timeout=max(0.01, 15.0 - (time.monotonic() - request_time)))
        state['normal_exit_confirmed'] = player.returncode == 0
        if not state['normal_exit_confirmed']:
            raise RuntimeError('Nonzero exit after normal quit request')
        state['result'] = 'PASS_NORMAL_EXIT'
    except BaseException as exc:
        state['result'] = 'FAIL_OR_INCOMPLETE'
        state['reason'] = type(exc).__name__ + ': ' + str(exc)
        progress(run, 'probe_failed', reason=state['reason'])
    finally:
        # Only the retained unreaped Popen child is signalled; never name/PID-group kill.
        if player is not None:
            try:
                if player.poll() is None:
                    state['forced_cleanup'] = True
                    state['signals'].append('SIGTERM')
                    progress(run, 'cleanup_sigterm', pid=player.pid)
                    player.terminate()
                    try:
                        player.wait(timeout=5.0)
                    except subprocess.TimeoutExpired:
                        state['signals'].append('SIGKILL')
                        progress(run, 'cleanup_sigkill', pid=player.pid)
                        player.kill()
                        player.wait(timeout=5.0)
                state['player_exit_code'] = player.poll()
                state['process_may_still_be_running'] = player.returncode is None
            except BaseException as exc:
                state['cleanup_error'] = type(exc).__name__ + ': ' + str(exc)
                state['process_may_still_be_running'] = player.poll() is None
            if stream is not None:
                stream.close()
            if not state['process_may_still_be_running']:
                try:
                    if player_processes(budget, identity['executable']):
                        raise RuntimeError('Another same-name Player remains; do not restore while a Player is active')
                    restore_player(run, budget, before, protected_before, identity, state)
                except BaseException as exc:
                    state['restore_error'] = type(exc).__name__ + ': ' + str(exc)
                    state['result'] = 'RESTORE_REVIEW_REQUIRED'
        elif stream is not None:
            stream.close()
        if support_before is not None:
            try:
                support_after = support_manifest()
                dump_new(run / 'application-support-after.json', support_after)
                state['application_support_changes'] = file_changes(support_before, support_after)
            except BaseException as exc:
                state['support_manifest_error'] = type(exc).__name__ + ': ' + str(exc)
        findings = console_findings(run)
        dump_new(run / 'console-findings.json', findings)
        log_path = run / 'player.log'
        log = log_path.read_text(errors='replace') if log_path.exists() else ''
        modes = re.findall(r'kGfxThreadingMode\w+', log)
        state['graphics_threading_modes'] = modes
        state['force_gfx_direct_confirmed'] = bool(modes) and all(mode == 'kGfxThreadingModeDirect' for mode in modes)
        expected_mode = 'kGfxThreadingModeDirect' if graphics == 'direct' else 'kGfxThreadingModeThreaded'
        state['requested_mode_confirmed'] = bool(modes) and all(mode == expected_mode for mode in modes)
        state['console_exception_count'] = findings['count']
        if state['result'] == 'PASS_NORMAL_EXIT' and (not state['requested_mode_confirmed'] or findings['count']):
            state['result'] = 'NORMAL_EXIT_BUT_MODE_OR_LOG_REVIEW_REQUIRED'
        state['elapsed_seconds'] = round(time.monotonic() - budget.start, 3)
        state['within_60_seconds'] = state['elapsed_seconds'] < 60.0
        state['finished_utc'] = utc()
        dump_new(run / 'result.json', state)
        progress(run, 'finished', result=state['result'], pid=state['player_pid'],
                 normal_exit=state['normal_exit_confirmed'], forced_cleanup=state['forced_cleanup'],
                 restoration_pending=state['restoration_pending'], elapsed_seconds=state['elapsed_seconds'])
    return 0 if state['result'] == 'PASS_NORMAL_EXIT' and not state['restoration_pending'] else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--graphics', choices=['direct', 'threaded'], default='direct')
    parser.add_argument('--run-dir', type=Path, default=QA / 'standalone-direct-run1')
    args = parser.parse_args()
    run = args.run_dir.resolve()
    if run.parent != QA or run.exists():
        parser.error('--run-dir must be a new immediate child of this QA folder')
    if not args.execute:
        print(json.dumps({'prepared_only': True, 'run_dir': str(run), 'app': str(APP),
                          'reused_helper': str(HELPER), 'normal_quit_seconds': 15,
                          'observation_seconds': 20, 'sample_at_seconds': 5, 'sample_duration_seconds': 2,
                          'owned_child_cleanup': 'TERM wait5, then KILL wait5 if needed',
                          'fresh_full_domain_backup': True, 'maximum_budget_seconds': 60}, indent=2))
        return 0
    return probe(run, args.graphics)


if __name__ == '__main__':
    sys.exit(main())
