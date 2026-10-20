#!/usr/bin/env python3
"""Create/verify one NEW local candidate ZIP after root-approved package checks.
Does not launch the app, sign/notarize it, or publish anything. Root executes; preparation only.
"""
from __future__ import annotations
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile

QA = Path(__file__).resolve().parent
PROJECT = QA.parent.parent
BASE = Path('/private/tmp/GearCraftReleasePolish20261019')
APP = BASE / 'GearCraft.app'
ARCHIVE = BASE / 'GearCraft-macOS-universal-20261019-Polish.zip'
DEST = PROJECT / 'Builds/ReleaseCandidate20261019-Polish'


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def command(args, timeout=90):
    proc = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
    return {'argv': args, 'exit_code': proc.returncode, 'stdout': proc.stdout.strip(), 'stderr': proc.stderr.strip()}


def tree_manifest(root):
    rows = {}
    def inspect(path):
        entry = path.lstat()
        name = '.' if path == root else path.relative_to(root).as_posix()
        row = {'mode': stat.S_IMODE(entry.st_mode)}
        if stat.S_ISLNK(entry.st_mode):
            row.update(kind='symlink', target=os.readlink(path))
        elif stat.S_ISREG(entry.st_mode):
            row.update(kind='file', bytes=entry.st_size, sha256=digest(path))
        elif stat.S_ISDIR(entry.st_mode):
            row.update(kind='directory')
        else:
            raise RuntimeError('Unexpected filesystem object in app: ' + str(path))
        rows[name] = row
    inspect(root)
    for directory, dirs, files in os.walk(root, followlinks=False):
        for name in sorted(dirs + files):
            inspect(Path(directory) / name)
    return rows


def mismatches(before, after):
    return [{'path': name, 'before': before.get(name), 'after': after.get(name)}
            for name in sorted(set(before) | set(after)) if before.get(name) != after.get(name)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-report', type=Path, default=QA / 'macos-package-verification.json')
    parser.add_argument('--report', type=Path, default=QA / 'release-archive.json')
    args = parser.parse_args()
    report_path = args.report.resolve()
    if not report_path.is_relative_to(QA) or report_path.exists():
        parser.error('Report must be a new file in this QA; no overwrites')
    if ARCHIVE.exists() or DEST.exists():
        parser.error('New archive/destination already exists; never overwrite an earlier candidate')
    report = {'utc': dt.datetime.now(dt.timezone.utc).isoformat(), 'source_app': str(APP),
              'archive': str(ARCHIVE), 'workspace_destination': str(DEST), 'app_started': False,
              'scope': 'Local candidate creation and extraction integrity. Ad-hoc signature check only; no Developer ID, notarization, Gatekeeper acceptance, public release, or new UI/runtime claim.',
              'errors': []}
    before = None
    try:
        package_path = args.package_report.resolve()
        package = json.loads(package_path.read_text())
        if Path(package.get('app', '')).resolve() != APP.resolve() or package.get('status') != 'PASS_LOCAL_ARTIFACT_CHECKS_WITH_LIMITS' or package.get('errors'):
            raise RuntimeError('Require successful current-app package verification')
        report['package_verification'] = {'path': str(package_path), 'sha256': digest(package_path)}
        if not package.get('normal_release_excludes_audio_QA_harness', {}).get('managed_type_marker_absent'):
            raise RuntimeError('Normal candidate lacks proof of QA harness exclusion')
        if not package.get('normal_release_excludes_audio_QA_harness', {}).get('runtime_initializer_marker_absent'):
            raise RuntimeError('Normal candidate may include QA runtime initializer')
        source_manifest_path = QA / 'source-build-manifest.json'
        source_manifest = json.loads(source_manifest_path.read_text())
        source_map = source_manifest.get('production_and_settings_sha256', {})
        if len(source_map) != 18 or 'Assets/GearCraft/Scenes/Craft.unity' not in source_map:
            raise RuntimeError('Expected current 18-source manifest including Craft')
        for name, expected in source_map.items():
            if digest(PROJECT / name) != expected:
                raise RuntimeError('Source changed since manifest: ' + name)
        if digest(source_manifest_path) != package['source_manifest']['sha256']:
            raise RuntimeError('Package checker used a different source manifest')
        for name, expected in package['artifact_hashes']['after'].items():
            if digest(APP / name) != expected:
                raise RuntimeError('App changed since package verification: ' + name)
        before = tree_manifest(APP)
        # Candidate must remain free of the audio instrumentation, even if a stale report was supplied.
        if b'GearCraftQAAudioPersistence' in (APP / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll').read_bytes():
            raise RuntimeError('QA audio harness in candidate assembly')
        report['create_zip'] = command(['/usr/bin/ditto', '-c', '-k', '--sequesterRsrc', '--keepParent', str(APP), str(ARCHIVE)])
        if report['create_zip']['exit_code'] != 0:
            raise RuntimeError('ditto ZIP creation failed')
        with zipfile.ZipFile(ARCHIVE) as zipped:
            for entry in zipped.infolist():
                name = PurePosixPath(entry.filename)
                if name.is_absolute() or '..' in name.parts or name.parts[0] not in ('GearCraft.app', '__MACOSX'):
                    raise RuntimeError('Unexpected ZIP entry root/path: ' + entry.filename)
            broken = zipped.testzip()
            if broken:
                raise RuntimeError('ZIP CRC mismatch: ' + broken)
            report['zip_entries'] = len(zipped.infolist())
        extraction = Path(tempfile.mkdtemp(prefix='candidate-roundtrip-', dir=BASE))
        report['extraction_directory'] = str(extraction)
        report['extract_zip'] = command(['/usr/bin/ditto', '-x', '-k', str(ARCHIVE), str(extraction)])
        if report['extract_zip']['exit_code'] != 0:
            raise RuntimeError('ditto ZIP extraction failed')
        extracted = extraction / 'GearCraft.app'
        diff = mismatches(before, tree_manifest(extracted))
        report['roundtrip_mismatches'] = diff
        if diff:
            raise RuntimeError('Roundtrip app hashes/modes/symlink targets differ')
        report['extracted_signature'] = command(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(extracted)], timeout=30)
        if report['extracted_signature']['exit_code'] != 0:
            raise RuntimeError('Extracted bundle signature integrity failed')
        after = tree_manifest(APP)
        report['source_app_mismatches'] = mismatches(before, after)
        if report['source_app_mismatches']:
            raise RuntimeError('Source app changed while packaging')
        report['sha256'] = digest(ARCHIVE)
        report['bytes'] = ARCHIVE.stat().st_size
        report['app_files_and_symlinks'] = sum(row['kind'] != 'directory' for row in before.values())
        report['app_directories'] = sum(row['kind'] == 'directory' for row in before.values())
        DEST.mkdir(parents=True, exist_ok=False)
        workspace_archive = DEST / ARCHIVE.name
        with ARCHIVE.open('rb') as source, workspace_archive.open('xb') as dest:
            shutil.copyfileobj(source, dest, length=1024 * 1024)
        if digest(workspace_archive) != report['sha256']:
            raise RuntimeError('Workspace archive copy hash differs')
        with (DEST / (ARCHIVE.name + '.sha256')).open('x') as file:
            file.write(report['sha256'] + '  ' + ARCHIVE.name + '\n')
        with (DEST / 'README.txt').open('x') as file:
            file.write('GearCraft macOS 配布候補 — 2026-10-19 Polish\n\n'
                       '新しいGearCraft.appを格納したローカル候補です。以前のReleaseCandidate20261019は保持しています。\n'
                       'Intel / Apple Silicon両architectureと実コードmarker、ZIP展開後の全file hash・mode・link target、署名整合性を今回のQA記録で照合しています。\n'
                       '音量テスト用のQA harnessを含むAudioQA.appとは別の、通常候補です。\n\n'
                       'ローカルad-hoc署名の整合性を確認した候補であり、Developer ID署名・公証・Gatekeeper許可・正式公開を保証するものではありません。\n'
                       '実機動作・UI確認・既知の未解決事項はプロジェクト内QA/ReleasePolish20261019の最終報告を参照してください。\n'
                       'このZIP作成検査だけで終了時R22注意や全機能を合格扱いしません。\n')
        report['workspace_archive'] = str(workspace_archive)
        report['workspace_archive_sha256_verified'] = True
        report['roundtrip_all_file_hashes_modes_symlink_targets_match'] = True
        report['source_app_unchanged'] = True
        # Delete only our fresh extraction after successful checks. Preserve it on failure for inspection.
        shutil.rmtree(extraction)
        report['temporary_extraction_removed'] = True
    except Exception as error:
        report['errors'].append(type(error).__name__ + ': ' + str(error))
        if before is not None and APP.exists():
            try:
                report['source_app_mismatches'] = mismatches(before, tree_manifest(APP))
            except Exception as second:
                report['source_app_recheck_error'] = str(second)
    report['status'] = 'PASS' if not report['errors'] else 'FAIL_OR_UNVERIFIED'
    with report_path.open('x') as file:
        json.dump(report, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(report['status'] + '; report=' + str(report_path))
    return 0 if not report['errors'] else 1


if __name__ == '__main__':
    sys.exit(main())
