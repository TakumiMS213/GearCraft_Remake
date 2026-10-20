#!/usr/bin/env python3
"""Prepared QA-only two-Player Audio persistence test. No action without --execute.

Native Slider callbacks write, a separate process runs Audio.Start, and both
harness processes quit themselves. This is not physical OS input or listening.
Fresh full PlayerPrefs are restored after all owned Players have exited.
"""
from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time
import uuid

from audio_qa_common import (
    QA, APP, PLAYER_DOMAIN, PROTECTED_DOMAINS, PREFS, utc, digest, canonical,
    write_new, dump_new, progress, app_identity, support_manifest, file_changes,
    console_findings,
)

OUTPUT_BASE = Path('/private/tmp/GearCraftReleasePolish20261019/audio-persistence')
BUILD_IDENTITY = QA / 'audio-qa-build-identity.json'
CANDIDATE = QA.parent.parent / 'Builds/ReleaseCandidate20261019/GearCraft-macOS-universal-20261019.zip'
CANDIDATE_SHA256 = '26a4b9729b9d8f480dc26f2eb0da1e56e172386dbdf9105275f78186d29ebcba'
AUDIO_KEYS = {'bgm': 'GearCraft.Audio.BGM.dB.v1', 'se': 'GearCraft.Audio.SE.dB.v1'}
REQUESTED = {'bgm': -17.25, 'se': -28.75}
EXPECTED = {'bgm': -17.0, 'se': -29.0}


class Budget:
    def __init__(self):
        self.start = time.monotonic()
        self.deadline = self.start + 58.0  # Reserve one second within the total 59-second budget.

    def remaining(self):
        return self.deadline - time.monotonic()

    def run(self, args, timeout=1.5):
        available = self.remaining()
        if available < 0.1:
            raise TimeoutError('Overall 59-second budget exhausted')
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


def candidate_snapshot():
    if CANDIDATE.is_symlink() or not CANDIDATE.is_file():
        raise RuntimeError('Protected release candidate ZIP missing/not a regular file')
    value = digest(CANDIDATE.read_bytes())
    if value != CANDIDATE_SHA256:
        raise RuntimeError('Protected release candidate ZIP differs from reviewed hash')
    return {'path': str(CANDIDATE), 'bytes': CANDIDATE.stat().st_size, 'sha256': value}


def close_number(value, expected):
    return type(value) in (int, float) and math.isfinite(value) and abs(value - expected) < 0.001


def validate_result(value, mode, session, pid, write_pid):
    required = ['schema', 'session', 'mode', 'success', 'status', 'pid', 'exit_code_requested',
                'requested_bgm_db', 'requested_se_db', 'expected_bgm_db', 'expected_se_db',
                'mixer_bgm_db', 'mixer_se_db', 'slider_bgm_db', 'slider_se_db',
                'prefs_bgm_db', 'prefs_se_db', 'prefs_bgm_exists', 'prefs_se_exists',
                'bgm_callback_count', 'se_callback_count', 'native_pending_changes',
                'boot_bgm_exists', 'boot_se_exists', 'boot_bgm_db', 'boot_se_db', 'write_pid', 'message']
    if not isinstance(value, dict) or any(key not in value for key in required):
        raise RuntimeError('Incomplete harness schema for ' + mode)
    if (value['schema'] != 1 or value['session'] != session or value['mode'] != mode
            or value['success'] is not True or value['status'] != 'PASS_' + mode.upper()
            or value['pid'] != pid or value['exit_code_requested'] != 0
            or value['write_pid'] != write_pid or value['native_pending_changes'] is not False):
        raise RuntimeError('Harness identity/outcome/pending check failed for ' + mode)
    if mode == 'read' and pid == write_pid:
        raise RuntimeError('Read is not a distinct Player PID')
    for channel in ('bgm', 'se'):
        if not close_number(value['requested_' + channel + '_db'], REQUESTED[channel]):
            raise RuntimeError('Harness requested value mismatch')
        for kind in ('expected', 'mixer', 'slider', 'prefs'):
            if not close_number(value[kind + '_' + channel + '_db'], EXPECTED[channel]):
                raise RuntimeError('Harness ' + kind + '/' + channel + ' mismatch')
        if value['prefs_' + channel + '_exists'] is not True:
            raise RuntimeError('Harness persistent audio key missing')
        count = value[channel + '_callback_count']
        if type(count) is not int or (mode == 'write' and count < 1) or (mode == 'read' and count != 0):
            raise RuntimeError('Unexpected native UI callback count for ' + mode)
        if mode == 'read' and (value['boot_' + channel + '_exists'] is not True
                or not close_number(value['boot_' + channel + '_db'], EXPECTED[channel])):
            raise RuntimeError('Fresh process did not read expected saved value before Audio.Start')


def read_result(path, destination):
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 1024 * 1024:
        raise RuntimeError('Missing/invalid phase result: ' + str(path))
    data = path.read_bytes()
    write_new(destination, data)
    return json.loads(data)


def verify_saved_audio(snap):
    if snap[0]['disk_matches_export'] is not True:
        raise RuntimeError('Audio phase Player domain disk/cache mismatch')
    checked = {}
    for channel, key in AUDIO_KEYS.items():
        value = snap[3].get(key)
        if key not in snap[3] or not close_number(value, EXPECTED[channel]):
            raise RuntimeError('On-disk persisted audio value mismatch: ' + channel)
        checked[channel] = {'key': key, 'exists': True, 'value': value, 'plist_type': type(value).__name__}
    return checked


def probe(run, session):
    budget = Budget()
    run.mkdir(exist_ok=False)
    output = OUTPUT_BASE / session
    state = {'started_utc': utc(), 'result': 'NOT_STARTED', 'session': session,
             'scope': 'Two separate Player processes; native Slider callbacks then native Audio.Start. No physical OS input or listening.',
             'normal_exit_confirmed': False, 'forced_cleanup': False, 'signals': [],
             'process_may_still_be_running': False, 'restoration_pending': False,
             'player_domain_restoration': 'not_needed', 'phase_outcomes': {}, 'player_pids': [],
             'requested_values': REQUESTED, 'effective_native_whole_number_values': EXPECTED}
    player = stream = before = identity = protected_before = support_before = candidate_before = None
    launched_any = False
    try:
        identity = app_identity()
        if BUILD_IDENTITY.exists():
            if identity != json.loads(BUILD_IDENTITY.read_text()):
                raise RuntimeError('AudioQA app changed from this batch first identity')
        else:
            dump_new(BUILD_IDENTITY, identity)
        dump_new(run / 'app-before.json', identity)
        candidate_before = candidate_snapshot()
        dump_new(run / 'protected-release-candidate-before.json', candidate_before)
        existing = player_processes(budget, identity['executable'])
        dump_new(run / 'single-instance-before.json', {'matches': existing})
        if existing:
            raise RuntimeError('A same-name Player already exists; no launch')
        before = snapshot(PLAYER_DOMAIN, budget)
        record_snapshot(run, 'player-domain-before', before)
        if not before[0]['exists'] or before[0]['disk_matches_export'] is not True:
            raise RuntimeError('Fresh Player domain missing or cache/disk mismatch; no launch')
        protected_before = protected(budget)
        dump_new(run / 'protected-domains-before.json', protected_before)
        if any(value['disk_matches_export'] is False for value in protected_before.values()):
            raise RuntimeError('Protected Editor domain disk/cache mismatch; no launch')
        support_before = support_manifest()
        dump_new(run / 'application-support-before.json', support_before)
        if app_identity() != identity:
            raise RuntimeError('AudioQA app changed before launch')
        if time.monotonic() - budget.start > 5.0:
            raise TimeoutError('Preparation exceeded five seconds; no launch')
        if OUTPUT_BASE.exists() and (OUTPUT_BASE.is_symlink() or not OUTPUT_BASE.is_dir()):
            raise RuntimeError('Audio QA output base is not a real directory')
        OUTPUT_BASE.mkdir(parents=True, exist_ok=True)
        output.mkdir(exist_ok=False)
        dump_new(run / 'backup-ready.json', {'utc': utc(), 'fresh_full_player_domain': True,
                 'session': session, 'harness_output': str(output), 'maximum_total_seconds': 59})
        write_pid = None
        for mode in ('write', 'read'):
            if budget.remaining() < 13.0:
                raise TimeoutError('Not enough budget for another phase plus restoration')
            if app_identity() != identity or player_processes(budget, identity['executable']):
                raise RuntimeError('App changed or another Player appeared before phase')
            if protected(budget) != protected_before or support_manifest() != support_before:
                raise RuntimeError('Protected Editor/AppSupport state changed between phases')
            phase = run / mode
            phase.mkdir(exist_ok=False)
            if any((output / (mode + suffix)).exists() for suffix in ('-result.json', '-events.jsonl')):
                raise RuntimeError('Harness output already exists; no overwrite or launch')
            command = [identity['executable'], '-screen-fullscreen', '0', '-screen-width', '1280',
                       '-screen-height', '720', '-logFile', str(phase / 'player.log'),
                       '-gearcraft-qa-audio', mode, '-gearcraft-qa-session', session]
            stream = (phase / 'player-stdio.log').open('xb')
            player = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
            launched_any = True
            started = time.monotonic()
            phase_deadline = min(started + 20.0, budget.deadline - 12.0)
            state['player_pids'].append(player.pid)
            state.update(process_may_still_be_running=True, restoration_pending=True,
                         player_domain_restoration='pending')
            dump_new(phase / 'launched-process.json', {'utc': utc(), 'pid': player.pid, 'command': command})
            progress(run, 'phase_started', mode=mode, pid=player.pid)
            owned = player_processes(budget, identity['executable'])
            if len(owned) != 1 or owned[0]['pid'] != player.pid or Path(owned[0]['executable']).resolve() != Path(identity['executable']).resolve():
                raise RuntimeError('Phase Player ownership/single-instance check failed')
            next_progress = started + 3.0
            while player.poll() is None:
                now = time.monotonic()
                if now >= phase_deadline:
                    raise TimeoutError(mode + ' phase did not quit within its 20-second/restoration-safe deadline')
                if now >= next_progress:
                    progress(run, 'phase_waiting', mode=mode, pid=player.pid, elapsed_seconds=round(now - started, 3))
                    next_progress = now + 3.0
                time.sleep(0.05)
            stream.close()
            stream = None
            state['process_may_still_be_running'] = False
            value = read_result(output / (mode + '-result.json'), phase / 'harness-result.json')
            state['phase_outcomes'][mode] = {'pid': player.pid, 'exit_code': player.returncode,
                'elapsed_seconds': round(time.monotonic() - started, 3), 'harness_status': value.get('status'),
                'verified': False}
            if player.returncode != 0:
                raise RuntimeError(mode + ' Player did not exit normally with code 0')
            if mode == 'write':
                write_pid = player.pid
            validate_result(value, mode, session, player.pid, write_pid)
            snap = snapshot(PLAYER_DOMAIN, budget)
            record_snapshot(run, 'player-domain-after-' + mode, snap)
            checked = verify_saved_audio(snap)
            dump_new(phase / 'disk-audio-verification.json', checked)
            if player_processes(budget, identity['executable']):
                raise RuntimeError('Another Player appeared after phase; no next launch')
            state['phase_outcomes'][mode]['verified'] = True
            progress(run, 'phase_passed', mode=mode, pid=player.pid)
        state['normal_exit_confirmed'] = True
        state['result'] = 'PASS_TWO_PROCESS_AUDIO_PERSISTENCE'
    except BaseException as exc:
        state['result'] = 'FAIL_OR_INCOMPLETE'
        state['reason'] = type(exc).__name__ + ': ' + str(exc)
        progress(run, 'probe_failed', reason=state['reason'])
    finally:
        if player is not None:
            try:
                # Only this retained, unreaped Popen child; no PID/name/group kill.
                if player.poll() is None:
                    state['forced_cleanup'] = True
                    state['signals'].append({'pid': player.pid, 'signal': 'SIGTERM'})
                    player.terminate()
                    try:
                        player.wait(timeout=2.0)
                    except subprocess.TimeoutExpired:
                        state['signals'].append({'pid': player.pid, 'signal': 'SIGKILL'})
                        player.kill()
                        player.wait(timeout=2.0)
                state['process_may_still_be_running'] = player.poll() is None
            except BaseException as exc:
                state['cleanup_error'] = type(exc).__name__ + ': ' + str(exc)
                state['process_may_still_be_running'] = player.poll() is None
        if stream is not None:
            stream.close()
        if launched_any and not state['process_may_still_be_running']:
            try:
                if player_processes(budget, identity['executable']):
                    raise RuntimeError('Same-name Player remains; no restoration while Player active')
                restore_player(run, budget, before, protected_before, identity, state)
            except BaseException as exc:
                state['restore_error'] = type(exc).__name__ + ': ' + str(exc)
                state['result'] = 'RESTORE_REVIEW_REQUIRED'
        if support_before is not None:
            try:
                after = support_manifest()
                dump_new(run / 'application-support-after.json', after)
                state['application_support_changes'] = file_changes(support_before, after)
                state['application_support_unchanged'] = not any(state['application_support_changes'].values())
            except BaseException as exc:
                state['support_error'] = type(exc).__name__ + ': ' + str(exc)
        if candidate_before is not None:
            try:
                after = candidate_snapshot()
                dump_new(run / 'protected-release-candidate-after.json', after)
                state['protected_release_candidate_unchanged'] = after == candidate_before
            except BaseException as exc:
                state['candidate_error'] = type(exc).__name__ + ': ' + str(exc)
                state['protected_release_candidate_unchanged'] = False
        findings = {}
        for mode in ('write', 'read'):
            phase = run / mode
            if phase.is_dir():
                findings[mode] = console_findings(phase)
        dump_new(run / 'console-findings.json', findings)
        state['console_exception_count'] = sum(value['count'] for value in findings.values())
        state['known_termination_notice_count'] = sum(value['known_termination_notice_count'] for value in findings.values())
        state['elapsed_seconds'] = round(time.monotonic() - budget.start, 3)
        state['within_59_seconds'] = state['elapsed_seconds'] < 59.0
        if state['result'] == 'PASS_TWO_PROCESS_AUDIO_PERSISTENCE' and (
                state['forced_cleanup'] or state['restoration_pending'] or state['process_may_still_be_running']
                or not state['within_59_seconds'] or not state.get('application_support_unchanged')
                or not state.get('protected_release_candidate_unchanged') or state['console_exception_count']):
            state['result'] = 'PHASES_PASS_BUT_FINAL_VERIFICATION_INCOMPLETE'
        state['finished_utc'] = utc()
        dump_new(run / 'result.json', state)
        progress(run, 'finished', result=state['result'], restoration_pending=state['restoration_pending'],
                 process_may_still_be_running=state['process_may_still_be_running'], elapsed_seconds=state['elapsed_seconds'])
    return 0 if state['result'] == 'PASS_TWO_PROCESS_AUDIO_PERSISTENCE' else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--run-dir', type=Path, default=QA / 'audio-persistence-run1')
    parser.add_argument('--session', help='Optional fresh 32-character alphanumeric nonce; otherwise UUID hex')
    args = parser.parse_args()
    run = args.run_dir.resolve()
    if run.parent != QA or run.exists():
        parser.error('--run-dir must be a new immediate child of this QA folder')
    session = args.session or uuid.uuid4().hex
    if not re.fullmatch(r'[A-Za-z0-9]{32}', session):
        parser.error('--session must contain exactly 32 ASCII alphanumeric characters')
    if not args.execute:
        print(json.dumps({'prepared_only': True, 'app': str(APP), 'run_dir': str(run),
            'session': session, 'harness_output': str(OUTPUT_BASE / session),
            'phase_seconds_max': 20, 'total_seconds_max': 59,
            'requested': REQUESTED, 'expected_native_whole_number_values': EXPECTED,
            'fresh_full_domain_restore': True, 'physical_os_input': False}, indent=2))
        return 0
    return probe(run, session)


if __name__ == '__main__':
    sys.exit(main())

