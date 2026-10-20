#!/usr/bin/env python3
"""Read-only macOS package audit; writes only one new QA report, never starts the app.
Run by root AFTER the new build has finished. No signing, notarization, import, or prefs access.
"""
from __future__ import annotations
import argparse
import base64
import datetime as dt
import hashlib
import json
import mmap
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys

QA = Path(__file__).resolve().parent
PROJECT = QA.parent.parent
UNITY = Path('/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents')
MONO = UNITY / 'MonoBleedingEdge/bin/mono'
CECIL = UNITY / 'Tools/Compilation/ApiUpdater/Mono.Cecil.dll'


def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as file:
        for block in iter(lambda: file.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def run(argv: list[str], timeout: float = 20, env=None) -> dict:
    try:
        proc = subprocess.run(argv, capture_output=True, text=True, timeout=timeout, env=env)
        return {'argv': argv, 'exit_code': proc.returncode, 'stdout': proc.stdout.strip(), 'stderr': proc.stderr.strip()}
    except subprocess.TimeoutExpired as error:
        return {'argv': argv, 'exit_code': None, 'timeout_seconds': timeout, 'error': str(error)}
    except OSError as error:
        return {'argv': argv, 'exit_code': None, 'error': str(error)}


def source_snapshot(manifest: dict) -> list[dict]:
    rows = []
    for name, expected in manifest.get('production_and_settings_sha256', {}).items():
        path = (PROJECT / name).resolve()
        if not path.is_relative_to(PROJECT):
            raise ValueError('Manifest path escapes project')
        actual = sha(path) if path.is_file() else None
        rows.append({'path': name, 'expected_sha256': expected, 'actual_sha256': actual, 'matches': actual == expected})
    return rows


def method_reference_matches(full_name: str, target: str) -> bool:
    """Cecil prints generic calls as Method<T>(...), non-generic as Method(...)."""
    if target.endswith('('):
        # Require the exact method name boundary, not SetLinkOther or another prefix.
        return re.search(re.escape(target[:-1]) + r'(?:\(|<)', full_name) is not None
    return target in full_name


def managed_markers(path: Path) -> dict:
    env = os.environ.copy()
    env['MONO_PATH'] = str(CECIL.parent)
    result = run([str(MONO), str(QA / 'ReadManagedMarkers.exe'), str(path)], env=env)
    if result['exit_code'] != 0:
        return {'status': 'UNVERIFIED', 'reader': result}
    rows = []
    for line in result.pop('stdout').splitlines():
        columns = line.split('\t')
        kind = columns[0]
        decoded = [base64.b64decode(x).decode('utf-8') for x in columns[1:-1]] if kind == 'CALL' else [base64.b64decode(x).decode('utf-8') for x in columns[1:]]
        if kind == 'CALL':
            decoded.append(int(columns[-1]))
        rows.append({'kind': kind, 'values': decoded})
    calls = [row['values'] for row in rows if row['kind'] == 'CALL']
    constants = [row['values'] for row in rows if row['kind'] == 'CONSTANT']
    strings = [row['values'] for row in rows if row['kind'] == 'STRING']
    types = [row['values'][0] for row in rows if row['kind'] == 'TYPE']

    def call(owner: str, target: str, method: str | None = None) -> list:
        return [r for r in calls if (r[0] == owner or r[0].startswith(owner + '/')) and method_reference_matches(r[2], target) and (method is None or method in r[1] or method in r[0])]

    checks = {
        'Ending.Start.links_owner': bool(call('EndingManager', 'DG.Tweening.TweenSettingsExtensions::SetLink(', '<Start>')),
        'Ending.ShowEndingAsync.links_owner': bool(call('EndingManager', 'DG.Tweening.TweenSettingsExtensions::SetLink(', '<ShowEndingAsync>')),
        'Ending.FadeOut.links_owner': bool(call('EndingManager', 'DG.Tweening.TweenSettingsExtensions::SetLink(', '<FadeOutAndLoadScene>')),
        'Ending.FadeOut.destroy_token': bool(call('EndingManager', '::GetCancellationTokenOnDestroy(', '<FadeOutAndLoadScene>')),
        'Ending.FadeOut.suppresses_cancel_throw': bool(call('EndingManager', '::SuppressCancellationThrow(', '<FadeOutAndLoadScene>')),
        'Ending.FadeOut.post_wait_token_query': bool(call('EndingManager', '::get_IsCancellationRequested(', '<FadeOutAndLoadScene>')),
        'Audio.native_float_restore': bool(call('Audio', 'UnityEngine.PlayerPrefs::GetFloat(')),
        'Audio.native_float_write': bool(call('Audio', 'UnityEngine.PlayerPrefs::SetFloat(')),
        'Audio.native_save': bool(call('Audio', 'UnityEngine.PlayerPrefs::Save(')),
        'Audio.unscaled_clock': bool(call('Audio', 'UnityEngine.Time::get_unscaledTime(')),
        'Audio.BGM.key': any(r[-1] == 'GearCraft.Audio.BGM.dB.v1' for r in constants + strings),
        'Audio.SE.key': any(r[-1] == 'GearCraft.Audio.SE.dB.v1' for r in constants + strings),
        'Cadence.type': 'PlayerAttackCadence' in types,
        'Cadence.automatic_interval': bool(call('PlayerAttackCadence', 'PlayerAttackCadence::GetAutomaticInterval(')),
        'Controller.shared_input_to_cadence': bool(call('PlayerController', 'PlayerAttackCadence::Advance(', 'ProcessAttackInput')),
    }
    selected = [r for r in calls if r[0].startswith('EndingManager') or r[0] == 'Audio' or 'PlayerAttackCadence' in r[2] or 'ProcessAttackInput' in r[2]]
    return {'status': 'MARKERS_PRESENT' if all(checks.values()) else 'MARKER_MISMATCH', 'checks': checks,
            'call_sites': selected, 'constants': constants, 'assembly_metadata': [r for r in rows if r['kind'] == 'ASSEMBLY'],
            'reader': result, 'reader_exe_sha256': sha(QA / 'ReadManagedMarkers.exe'),
            'scope': 'Static Cecil metadata/IL references in the built assembly; target DLL is not executed. Call presence does not prove all control flow, numeric arguments, UI behavior, or runtime cancellation timing.'}


def scalar(text: str) -> str:
    return json.loads(text) if text.startswith('"') else text


def yaml_documents(path: Path) -> list[dict]:
    text = path.read_text()
    result = []
    for match in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S):
        result.append({'class': int(match[1]), 'id': int(match[2]), 'text': match[3]})
    return result


def rect_source(path: Path, object_name: str) -> list[dict]:
    docs = yaml_documents(path)
    ids = []
    for doc in docs:
        m = re.search(r'^  m_Name: (.*)$', doc['text'], re.M)
        if doc['class'] == 1 and m and scalar(m[1]) == object_name:
            ids.append(doc['id'])
    result = []
    for doc in docs:
        m = re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}', doc['text'], re.M)
        if doc['class'] != 224 or not m or int(m[1]) not in ids:
            continue
        values = {}
        for field in ('m_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot'):
            found = re.search(r'^  ' + field + r': (.*)$', doc['text'], re.M)
            values[field] = found[1] if found else None
        result.append({'source_object': object_name, 'rect_file_id': doc['id'], 'values': values})
    return result


def embedded_strings(data: Path, needles: dict[str, str]) -> dict:
    found = {name: [] for name in needles}
    files = sorted(p for p in data.iterdir() if p.is_file() and (
        re.fullmatch(r'level\d+', p.name) or p.name.endswith('.assets') or p.name in ('globalgamemanagers', 'data.unity3d')))
    scanned = []
    for path in files:
        if path.stat().st_size == 0:
            continue
        scanned.append({'file': str(path.relative_to(data)), 'bytes': path.stat().st_size})
        with path.open('rb') as file, mmap.mmap(file.fileno(), 0, access=mmap.ACCESS_READ) as blob:
            for name, value in needles.items():
                raw = value.encode('utf-8')
                if not raw:
                    continue
                offset = blob.find(raw)
                if offset >= 0:
                    found[name].append({'file': str(path.relative_to(data)), 'first_utf8_offset': offset})
    return {'matches': found, 'scanned_files': scanned,
            'scope': 'Exact UTF-8 byte occurrence in packaged Unity data, not a parsed asset-object binding or visual proof. Compressed data may hide text; not-found alone is inconclusive.'}


def report_claims(path: Path) -> dict:
    result = {'path': str(path), 'sha256': sha(path)}
    try:
        value = json.loads(path.read_text())
    except (ValueError, UnicodeError):
        result['parse'] = 'Not JSON; retained by path/hash only'
        return result
    claims = []
    def walk(node, prefix=''):
        if isinstance(node, dict):
            for key, val in node.items():
                name = prefix + '.' + key if prefix else key
                if not isinstance(val, (dict, list)) and re.search(r'backend|il2cpp|mono|platform|target|options|development|result|status|outputPath|output_path|totalErrors|totalWarnings', key, re.I):
                    claims.append({'field': name, 'value': val})
                walk(val, name)
        elif isinstance(node, list):
            for i, val in enumerate(node):
                walk(val, prefix + '[' + str(i) + ']')
    walk(value)
    result['claims'] = claims[:80]
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=Path('/private/tmp/GearCraftReleasePolish20261019/GearCraft.app'))
    parser.add_argument('--manifest', type=Path, default=QA / 'source-build-manifest.json')
    parser.add_argument('--output', type=Path, default=QA / 'macos-package-verification.json')
    parser.add_argument('--build-report', type=Path, action='append', default=[])
    parser.add_argument('--expected-source-count', type=int, default=18)
    args = parser.parse_args()
    app, manifest_path, output = args.app.resolve(), args.manifest.resolve(), args.output.resolve()
    if not output.is_relative_to(QA) or output.exists():
        parser.error('Report must be a new file within this QA folder; refuse overwrite')
    if not app.is_dir() or not manifest_path.is_file():
        parser.error('Build app and source manifest must exist; run only AFTER build completion')
    report = {'utc': dt.datetime.now(dt.timezone.utc).isoformat(), 'app': str(app),
              'app_started': False, 'playerprefs_accessed': False, 'writes': [str(output)],
              'scope': 'Local artifact/source integrity only. Ad-hoc signature validity is not Developer ID signing, notarization, Gatekeeper acceptance, app launch, rendered layout, or R22 resolution.',
              'errors': [], 'warnings': []}
    try:
        manifest_sha_before = sha(manifest_path)
        manifest = json.loads(manifest_path.read_text())
        before = source_snapshot(manifest)
        report['source_manifest'] = {'path': str(manifest_path), 'sha256': manifest_sha_before,
                                     'expected_count': args.expected_source_count, 'actual_count': len(before), 'before': before}
        if len(before) != args.expected_source_count or not all(row['matches'] for row in before):
            report['errors'].append('Source manifest count/hash mismatch before artifact inspection')
        info_path = app / 'Contents/Info.plist'
        info = plistlib.loads(info_path.read_bytes())
        report['Info_plist'] = {key: info.get(key) for key in ('CFBundleIdentifier', 'CFBundleExecutable', 'CFBundleName', 'CFBundleShortVersionString', 'CFBundleVersion', 'LSMinimumSystemVersion', 'NSHighResolutionCapable')}
        executable = app / 'Contents/MacOS' / str(info['CFBundleExecutable'])
        if not executable.resolve().is_relative_to(app):
            raise ValueError('Executable path escapes app bundle')
        data = app / 'Contents/Resources/Data'
        managed = data / 'Managed/Assembly-CSharp.dll'
        mono_lib = app / 'Contents/Frameworks/libmonobdwgc-2.0.dylib'
        unity_player = app / 'Contents/Frameworks/UnityPlayer.dylib'
        tracked = [info_path, executable, managed, mono_lib, unity_player]
        hashes_before = {str(p.relative_to(app)): sha(p) for p in tracked if p.is_file()}
        report['signature_verify'] = run(['/usr/bin/codesign', '--verify', '--deep', '--strict', '--verbose=2', str(app)], timeout=30)
        report['signature_details'] = run(['/usr/bin/codesign', '-dv', '--verbose=4', str(app)])
        signature = report['signature_details'].get('stderr', '') + report['signature_details'].get('stdout', '')
        report['signature_classification'] = {'ad_hoc_marker': 'Signature=adhoc' in signature, 'notarization_checked': False, 'Developer_ID_verified': False}
        if report['signature_verify']['exit_code'] != 0:
            report['errors'].append('codesign deep strict verification failed/timed out')
        report['architectures'] = []
        for binary in (executable, unity_player, mono_lib):
            row = {'path': str(binary.relative_to(app)), 'exists': binary.is_file()}
            if binary.is_file():
                row['file'] = run(['/usr/bin/file', '-b', str(binary)])
                row['lipo'] = run(['/usr/bin/lipo', '-archs', str(binary)])
                archs = set(row['lipo'].get('stdout', '').split())
                row['arm64_and_x86_64'] = {'arm64', 'x86_64'} <= archs
                if not row['arm64_and_x86_64']:
                    report['errors'].append('Required universal architecture not observed: ' + row['path'])
            else:
                report['errors'].append('Expected native component missing: ' + row['path'])
            report['architectures'].append(row)
        il2cpp_files = [str(p.relative_to(app)) for name in ('GameAssembly.dylib', 'global-metadata.dat') for p in app.rglob(name)]
        report['runtime_evidence'] = {'mono_runtime_library_exists': mono_lib.is_file(), 'managed_game_assembly_exists': managed.is_file(), 'il2cpp_artifacts_found': il2cpp_files,
                                      'classification': 'MONO_PACKAGE_EVIDENCE' if mono_lib.is_file() and managed.is_file() and not il2cpp_files else 'MIXED_OR_INCONCLUSIVE',
                                      'note': 'Observed files take precedence over BuildReport backend/options labels. App has not been launched by this checker.'}
        if managed.is_file():
            raw_managed = managed.read_bytes()
            qa_type_absent = b'GearCraftQAAudioPersistence' not in raw_managed
            initializers = data / 'RuntimeInitializeOnLoads.json'
            initializer_absent = not initializers.is_file() or 'GearCraftQAAudioPersistence' not in initializers.read_text(errors='replace')
            report['normal_release_excludes_audio_QA_harness'] = {'managed_type_marker_absent': qa_type_absent, 'runtime_initializer_marker_absent': initializer_absent}
            if not qa_type_absent or not initializer_absent:
                report['errors'].append('Instrumented Audio QA harness found in normal release candidate')
            report['managed_markers'] = managed_markers(managed)
            if report['managed_markers']['status'] != 'MARKERS_PRESENT':
                report['errors'].append('Managed native method markers unverified or mismatched')
        else:
            report['errors'].append('Built Assembly-CSharp.dll missing')
        editor_dll = PROJECT / 'Library/ScriptAssemblies/Assembly-CSharp.dll'
        report['managed_hash_relationship'] = {'built_sha256': sha(managed) if managed.is_file() else None, 'editor_sha256': sha(editor_dll) if editor_dll.is_file() else None,
                                              'equal_required': False, 'reason': 'Release/player compilation, stripping and symbols differ from Editor compilation; byte equality is not a correctness criterion.'}
        boot = data / 'boot.config'
        report['boot_config'] = {'present': boot.is_file(), 'text': boot.read_text(errors='replace') if boot.is_file() else None,
                                 'scope': 'Literal packaged settings only; do not infer a full DevelopmentBuild classification from one key.'}
        title = PROJECT / 'Assets/GearCraft/Scenes/Title.unity'
        ending = PROJECT / 'Assets/GearCraft/Scenes/Ending.unity'
        craft = PROJECT / 'Assets/GearCraft/Scenes/Craft.unity'
        arrival = PROJECT / 'Assets/GearCraft/ScriptableObject/Story/CraftSpaceArrivalMessage.asset'
        pages = [json.loads(m[1]) for m in re.finditer(r'^    text: (".*")$', arrival.read_text(), re.M)]
        needles = {'Title.source_scene_path': 'Assets/GearCraft/Scenes/Title.unity', 'Title.credit_object': 'bgmTitle_title',
                   'Ending.source_scene_path': 'Assets/GearCraft/Scenes/Ending.unity', 'Ending.notice_object': 'GalleryUnlockNotice', 'Ending.hint_object': 'ContinueHint',
                   'Arrival.asset_name': 'CraftSpaceArrivalMessage', 'Craft.source_scene_path': 'Assets/GearCraft/Scenes/Craft.unity', 'Craft.shop_refresh_object': 'RefreshButton'}
        needles.update({'Arrival.page_' + str(i + 1): text for i, text in enumerate(pages)})
        report['scene_data'] = embedded_strings(data, needles)
        report['scene_data']['requested_markers'] = needles
        report['scene_data']['source_layout_only'] = {'Title_credit': rect_source(title, 'bgmTitle_title'), 'Ending_continue_hint': rect_source(ending, 'ContinueHint'), 'Craft_shop_refresh': rect_source(craft, 'RefreshButton'), 'Craft_viewports_all_matching_names': rect_source(craft, 'Viewport')}
        report['scene_data']['layout_values_verified_in_player'] = False
        report['scene_data']['layout_limit'] = 'Source hashes prove inspected source stayed unchanged; exact text/name matches prove those bytes are packaged. RectTransform numeric binding and actual screen layout still require a serialized asset parser or this new Player visual inspection. Prior Editor screenshots are not Player screenshots.'
        for key in needles:
            if not report['scene_data']['matches'][key]:
                report['warnings'].append('Packaged raw UTF-8 marker not found (may be compressed/renamed): ' + key)
        report['BuildReports'] = {'provided': [report_claims(path.resolve()) for path in args.build_report],
                                  'authority': 'Build tool claims only. Do not replace artifact backend, signature, architecture, source, or code-marker evidence with options flags; an IL2CPP flag does not override observed Mono files.'}
        if not args.build_report:
            report['warnings'].append('No BuildReport supplied; pass --build-report PATH to record claims alongside actual artifacts')
        after = source_snapshot(manifest)
        report['source_manifest']['after'] = after
        report['source_manifest']['manifest_file_unchanged'] = sha(manifest_path) == manifest_sha_before
        if not all(r['matches'] for r in after) or not report['source_manifest']['manifest_file_unchanged']:
            report['errors'].append('Source/manifest changed during check')
        hashes_after = {str(p.relative_to(app)): sha(p) for p in tracked if p.is_file()}
        report['artifact_hashes'] = {'before': hashes_before, 'after': hashes_after, 'unchanged': hashes_before == hashes_after}
        if hashes_before != hashes_after:
            report['errors'].append('Artifact changed during inspection; do not run against an in-progress build')
    except Exception as error:
        report['errors'].append(type(error).__name__ + ': ' + str(error))
    report['status'] = 'PASS_LOCAL_ARTIFACT_CHECKS_WITH_LIMITS' if not report['errors'] else 'FAIL_OR_UNVERIFIED'
    report['R22'] = 'UNRESOLVED: no process launch/termination or buffer allocation ownership tested here'
    with output.open('x') as file:
        json.dump(report, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(report['status'] + '; errors=' + str(len(report['errors'])) + '; warnings=' + str(len(report['warnings'])) + '; report=' + str(output))
    return 0 if not report['errors'] else 1


if __name__ == '__main__':
    sys.exit(main())
