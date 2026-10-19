#!/usr/bin/env python3
"""Reviewed QA-only macOS launch/normal-quit smoke. Does nothing without --execute.

No input/screen APIs or permission changes. Never kills a process. An uncertain
exit/identity/protected-domain state leaves PlayerPrefs untouched for root review.
This is not a UI test or an audio-persistence-across-restart test.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import plistlib
import subprocess
import sys
import time

QA = Path(__file__).resolve().parent
APP = Path('/private/tmp/GearCraftReleaseFinalization20261018/GearCraft.app')
PLAYER_DOMAIN = 'com.DefaultCompany.2D-URP'
PROTECTED_DOMAINS = ('unity.Takumi.GearCraft_Remake', 'com.Takumi.GearCraft_Remake')
PREFS = Path.home() / 'Library/Preferences'
SUPPORT = Path.home() / 'Library/Application Support/Takumi/GearCraft_Remake'


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    # Binary plist preserves bool/int/float/data/date types and sorts dictionary keys.
    return plistlib.dumps(value, fmt=plistlib.FMT_BINARY, sort_keys=True)


def write_new(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def dump_new(path, value):
    write_new(path, (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode())


def progress(run, event, **fields):
    # Values from preference dictionaries are never included here.
    item = {'utc': utc(), 'event': event, **fields}
    line = json.dumps(item, ensure_ascii=False)
    print(line, flush=True)
    with (run / 'progress.jsonl').open('a', encoding='utf-8') as stream:
        stream.write(line + '\n')
        stream.flush()


def domain_snapshot(domain):
    path = PREFS / (domain + '.plist')
    existed = path.exists()
    raw = path.read_bytes() if existed else None
    result = subprocess.run(['/usr/bin/defaults', 'export', domain, '-'], capture_output=True)
    if result.returncode:
        raise RuntimeError('defaults export failed for ' + domain)
    values = plistlib.loads(result.stdout)
    if not isinstance(values, dict):
        raise RuntimeError('Non-dictionary preference domain: ' + domain)
    # Fail before launch if cached preferences and on-disk preferences disagree.
    disk_matches = None if raw is None else canonical(plistlib.loads(raw)) == canonical(values)
    return {
        'domain': domain, 'path': str(path), 'exists': existed,
        'raw_sha256': None if raw is None else digest(raw),
        'typed_sha256': digest(canonical(values)), 'key_count': len(values),
        'disk_matches_export': disk_matches,
    }, raw, result.stdout, values


def support_manifest():
    result = {'path': str(SUPPORT), 'exists': SUPPORT.exists(), 'files': {}}
    if not SUPPORT.exists():
        return result
    if not SUPPORT.is_dir() or SUPPORT.is_symlink():
        raise RuntimeError('Application Support target is not a regular directory')
    for base, dirs, files in os.walk(SUPPORT, followlinks=False):
        for name in sorted(dirs + files):
            path = Path(base) / name
            rel = path.relative_to(SUPPORT).as_posix()
            stat = path.lstat()
            if path.is_symlink():
                result['files'][rel] = {'type': 'symlink', 'target': os.readlink(path)}
            elif path.is_dir():
                result['files'][rel] = {'type': 'directory'}
            else:
                sha = hashlib.sha256()
                with path.open('rb') as stream:
                    for data in iter(lambda: stream.read(1024 * 1024), b''):
                        sha.update(data)
                result['files'][rel] = {'type': 'file', 'bytes': stat.st_size, 'sha256': sha.hexdigest()}
    return result


def file_changes(before, after):
    left, right = before['files'], after['files']
    return {
        'directory_existence_changed': before['exists'] != after['exists'],
        'added': sorted(set(right) - set(left)),
        'removed': sorted(set(left) - set(right)),
        'changed': sorted(k for k in set(left) & set(right) if left[k] != right[k]),
    }


def app_identity():
    info_path = APP / 'Contents/Info.plist'
    info_bytes = info_path.read_bytes()
    info = plistlib.loads(info_bytes)
    if info.get('CFBundleIdentifier') != PLAYER_DOMAIN:
        raise RuntimeError('Unexpected app Bundle Identifier')
    executable = APP / 'Contents/MacOS' / info['CFBundleExecutable']
    if executable.resolve().parent != (APP / 'Contents/MacOS').resolve():
        raise RuntimeError('Unexpected executable location')
    managed = APP / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll'
    managed_bytes = managed.read_bytes()
    if 'GearCraft.Audio.BGM.dB.v1'.encode('utf-16le') not in managed_bytes or b'PlayerAttackCadence' not in managed_bytes:
        raise RuntimeError('App does not contain the latest Audio/cadence changes')
    return {
        'app': str(APP), 'executable': str(executable.resolve()),
        'bundle_identifier': info['CFBundleIdentifier'],
        'info_sha256': digest(info_bytes), 'exe_sha256': digest(executable.read_bytes()),
        'managed_sha256': digest(managed_bytes),
    }


def console_findings(run):
    import re
    patterns = re.compile(r'\b(?:[A-Za-z_][\w.]*Exception|Exception)\b|Assertion failed|Fatal error|Crash!!!')
    entries = []
    for name in ('player.log', 'player-stdio.log'):
        path = run / name
        if path.exists():
            for number, line in enumerate(path.read_text(errors='replace').splitlines(), 1):
                match = patterns.search(line)
                if match:
                    entries.append({'file': name, 'line': number, 'kind': match.group(0)})
    return {'matches': entries, 'count': len(entries), 'not_a_complete_crash_detector': True}


def smoke(run):
    run.mkdir(parents=True, exist_ok=False)
    state = {
        'started_utc': utc(), 'scope': 'Standalone launch and normal termination only; no UI/audio restart verification',
        'player_pid': None, 'quit_helper_pid': None, 'normal_exit_confirmed': False,
        'player_domain_restoration': 'not_attempted', 'restoration_pending': False,
        'process_may_still_be_running': False,
    }
    player = None
    log_stream = None
    support_before = None
    protected_before = {}
    try:
        identity = app_identity()
        dump_new(run / 'app-before.json', identity)
        progress(run, 'preparing_quit_helper')
        helper = run / 'normal-quit-helper'
        with (run / 'swift-compile.log').open('xb') as output:
            compile_result = subprocess.run([
                '/usr/bin/xcrun', 'swiftc', '-module-cache-path', str(run / 'swift-module-cache'),
                str(QA / 'StandaloneNormalQuit.swift'), '-o', str(helper),
            ], stdout=output, stderr=subprocess.STDOUT)
        if compile_result.returncode:
            raise RuntimeError('Normal-quit helper compilation failed; app not launched')

        player_meta, player_raw, player_export, player_values = domain_snapshot(PLAYER_DOMAIN)
        dump_new(run / 'player-domain-before.json', player_meta)
        write_new(run / 'player-domain-before.export.plist', player_export)
        if player_raw is not None:
            write_new(run / 'player-domain-before.raw.plist', player_raw)
        # Current verified machine has this domain. An absent domain would need
        # deletion to restore absence, which is outside this import-only script.
        if not player_meta['exists'] or player_meta['disk_matches_export'] is not True:
            raise RuntimeError('Player domain absent or disk/cache differs; review before launch')
        for domain in PROTECTED_DOMAINS:
            metadata, _, _, _ = domain_snapshot(domain)
            if metadata['disk_matches_export'] is False:
                raise RuntimeError('Protected domain disk/cache differs; review before launch')
            protected_before[domain] = metadata
        dump_new(run / 'protected-domains-before.json', protected_before)
        support_before = support_manifest()
        dump_new(run / 'application-support-before.json', support_before)
        dump_new(run / 'backup-ready.json', {'utc': utc(), 'player_domain': PLAYER_DOMAIN, 'full_typed_domain': True})

        # Full path and owned child PID avoid bundle-ID-based termination of another app.
        command = [identity['executable'], '-screen-fullscreen', '0', '-screen-width', '1280',
                   '-screen-height', '720', '-logFile', str(run / 'player.log')]
        log_stream = (run / 'player-stdio.log').open('xb')
        player = subprocess.Popen(command, stdout=log_stream, stderr=subprocess.STDOUT, start_new_session=True)
        state['player_pid'] = player.pid
        state['process_may_still_be_running'] = True
        state['restoration_pending'] = True
        dump_new(run / 'launched-process.json', {'utc': utc(), 'pid': player.pid, 'command': command})
        progress(run, 'player_started', pid=player.pid)
        launched = time.monotonic()
        next_progress = 5
        while time.monotonic() - launched < 20:
            if player.poll() is not None:
                raise RuntimeError('Player exited before the normal termination request; inspect logs')
            elapsed = time.monotonic() - launched
            if elapsed >= next_progress:
                progress(run, 'observing_player', pid=player.pid, elapsed_seconds=round(elapsed, 1))
                next_progress += 5
            time.sleep(0.25)

        progress(run, 'requesting_normal_termination', pid=player.pid)
        request_start = time.monotonic()
        quit_process = subprocess.Popen([str(helper), str(player.pid), identity['executable']],
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        state['quit_helper_pid'] = quit_process.pid
        try:
            # communicate(timeout) leaves the helper alive on timeout; never kill it.
            quit_out, quit_err = quit_process.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            raise RuntimeError('Normal-quit helper did not respond; helper and Player PIDs retained')
        write_new(run / 'normal-quit-helper.stdout.txt', quit_out.encode())
        write_new(run / 'normal-quit-helper.stderr.txt', quit_err.encode())
        reply = json.loads(quit_out.strip())
        state['normal_quit_reply'] = reply
        if quit_process.returncode or reply.get('accepted') is not True or reply.get('pid') != player.pid:
            raise RuntimeError('Normal termination request was not accepted; no fallback attempted')
        next_progress = 3
        while player.poll() is None:
            elapsed = time.monotonic() - request_start
            if elapsed >= 15:
                raise RuntimeError('Player did not exit within 15 seconds; no force termination attempted')
            if elapsed >= next_progress:
                progress(run, 'waiting_for_normal_exit', pid=player.pid, elapsed_seconds=round(elapsed, 1))
                next_progress += 3
            time.sleep(0.2)
        state['player_exit_code'] = player.returncode
        state['process_may_still_be_running'] = False
        if player.returncode != 0:
            raise RuntimeError('Player exited with a nonzero status after quit request')
        state['normal_exit_confirmed'] = True
        progress(run, 'normal_exit_confirmed', pid=player.pid, exit_code=player.returncode)
        log_stream.close()
        log_stream = None
        # Readback is after observed process exit; no preference writes while it is alive.
        after_meta, after_raw, after_export, _ = domain_snapshot(PLAYER_DOMAIN)
        dump_new(run / 'player-domain-after.json', after_meta)
        write_new(run / 'player-domain-after.export.plist', after_export)
        if after_raw is not None:
            write_new(run / 'player-domain-after.raw.plist', after_raw)
        protected_after = {domain: domain_snapshot(domain)[0] for domain in PROTECTED_DOMAINS}
        dump_new(run / 'protected-domains-after.json', protected_after)
        if protected_after != protected_before:
            raise RuntimeError('Protected Editor/compatibility domain changed; no automatic restoration attempted')
        if app_identity() != identity:
            raise RuntimeError('App identity changed during smoke; no automatic restoration attempted')
        if after_meta['disk_matches_export'] is not True:
            raise RuntimeError('Player domain disk/cache differs after exit; review before restoration')

        restore = subprocess.run(['/usr/bin/defaults', 'import', PLAYER_DOMAIN,
                                  str(run / 'player-domain-before.export.plist')], capture_output=True)
        write_new(run / 'defaults-import.stderr.txt', restore.stderr)
        if restore.returncode:
            raise RuntimeError('Player-domain import failed; backup retained')
        state['player_domain_restoration'] = 'imported_waiting_for_readback'
        restored_meta, _, restored_export, restored_values = domain_snapshot(PLAYER_DOMAIN)
        dump_new(run / 'player-domain-restored.json', restored_meta)
        write_new(run / 'player-domain-restored.export.plist', restored_export)
        if canonical(restored_values) != canonical(player_values) or restored_meta['exists'] != player_meta['exists'] or restored_meta['disk_matches_export'] is not True:
            raise RuntimeError('Player-domain type/value/existence readback mismatch; manual review required')
        final_protected = {domain: domain_snapshot(domain)[0] for domain in PROTECTED_DOMAINS}
        dump_new(run / 'protected-domains-final.json', final_protected)
        if final_protected != protected_before:
            raise RuntimeError('Protected domain changed during restore verification')
        state['player_domain_restoration'] = 'verified_full_type_value_existence'
        state['restoration_pending'] = False
        findings = console_findings(run)
        dump_new(run / 'console-findings.json', findings)
        state['console_exception_count'] = findings['count']
        state['result'] = 'PASS_LAUNCH_NORMAL_EXIT' if findings['count'] == 0 else 'FAIL_CONSOLE_EXCEPTION'
    except BaseException as exc:
        state['result'] = 'STOPPED_REVIEW_REQUIRED'
        state['reason'] = type(exc).__name__ + ': ' + str(exc)
        if player is not None:
            state['player_exit_code'] = player.poll()
            state['process_may_still_be_running'] = player.returncode is None
        # No forceTerminate, signal, process.kill, preference fallback, or directory deletion.
        progress(run, 'review_required', pid=state['player_pid'], process_may_still_be_running=state['process_may_still_be_running'],
                 restoration_pending=state['restoration_pending'], reason=state['reason'])
    finally:
        if log_stream is not None:
            log_stream.close()
        if support_before is not None:
            try:
                support_after = support_manifest()
                dump_new(run / 'application-support-after.json', support_after)
                state['application_support_changes'] = file_changes(support_before, support_after)
                # Record only. Never delete files created or modified by the app.
            except BaseException as exc:
                state['application_support_review_required'] = type(exc).__name__ + ': ' + str(exc)
        if not (run / 'console-findings.json').exists():
            dump_new(run / 'console-findings.json', console_findings(run))
        state['finished_utc'] = utc()
        dump_new(run / 'result.json', state)
        progress(run, 'finished', result=state['result'], pid=state['player_pid'],
                 restoration_pending=state['restoration_pending'], process_may_still_be_running=state['process_may_still_be_running'])
    return 0 if state['result'] == 'PASS_LAUNCH_NORMAL_EXIT' and not state['restoration_pending'] else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute', action='store_true', help='Run after root review and required execution/file-write approval')
    parser.add_argument('--run-dir', type=Path, default=QA / 'standalone-smoke-run1', help='New directory directly under this QA folder')
    args = parser.parse_args()
    run = args.run_dir.resolve()
    if run.parent != QA or run == QA:
        parser.error('--run-dir must be a new immediate child of this QA folder')
    if not args.execute:
        print(json.dumps({'prepared_only': True, 'app': str(APP), 'run_dir': str(run),
                          'requires_root_review': True, 'player_domain': PLAYER_DOMAIN,
                          'launch_seconds': 20, 'normal_exit_timeout_seconds': 15,
                          'no_force_termination': True, 'not_ui_or_audio_restart_test': True}, indent=2))
        return 0
    if run.exists():
        parser.error('Refusing to overwrite an existing run directory')
    return smoke(run)


if __name__ == '__main__':
    sys.exit(main())
