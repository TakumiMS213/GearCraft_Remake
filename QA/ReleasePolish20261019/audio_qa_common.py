#!/usr/bin/env python3
"""QA-only helpers for this verification build. Import has no runtime side effects.

No launch/quit/restore entry point here. audio_persistence_probe.py owns execution.
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
APP = Path('/private/tmp/GearCraftReleasePolish20261019/AudioQA.app')
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
    if b'GearCraftQAAudioPersistence' not in managed_bytes or '-gearcraft-qa-audio'.encode('utf-16le') not in managed_bytes:
        raise RuntimeError('App does not contain the requested temporary Audio QA harness')
    data_root = APP / 'Contents/Resources/Data'
    scene_content = {}
    for name in ('globalgamemanagers', 'level0', 'sharedassets0.assets', 'resources.assets', 'boot.config'):
        path = data_root / name
        if path.is_file():
            scene_content[name] = digest(path.read_bytes())
    if 'globalgamemanagers' not in scene_content or 'level0' not in scene_content:
        raise RuntimeError('Expected scene build data missing; no launch')
    return {
        'scene_content_sha256': scene_content,
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
    notices = []
    warning_pattern = re.compile(r'Thread may have been prematurely finalized|ComputeBuffer.*(?:GC|garbage)|GarbageCollector disposing of ComputeBuffer', re.IGNORECASE)
    for name in ('player.log', 'player-stdio.log'):
        path = run / name
        if path.exists():
            for number, line in enumerate(path.read_text(errors='replace').splitlines(), 1):
                match = warning_pattern.search(line)
                if match:
                    notices.append({'file': name, 'line': number, 'kind': match.group(0)})
    return {'matches': entries, 'count': len(entries), 'known_termination_notices': notices,
            'known_termination_notice_count': len(notices),
            'not_a_complete_warning_or_crash_detector': True}
