"""Compile production store and run fault tests in a disposable directory; inspect static UI references."""
from pathlib import Path
import subprocess, tempfile, re, shutil

root = Path(__file__).resolve().parents[2]
qa = Path(__file__).resolve().parent
unity = Path('/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents')
compiler = [str(unity/'NetCoreRuntime/dotnet'), str(unity/'DotNetSdkRoslyn/csc.dll')]
with tempfile.TemporaryDirectory(prefix='gearcraft-autosave-check-') as directory:
    out = Path(directory)
    api = unity/'MonoBleedingEdge/lib/mono/4.8-api'
    json_dll = next((root/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(json_dll, out/json_dll.name)
    lines = ['-nologo', '-target:exe', '-nostdlib+', f'-out:"{out}/tests.exe"']
    lines += [f'-r:"{api/name}"' for name in ['mscorlib.dll','System.dll','System.Core.dll']]
    lines += [f'-r:"{api}/Facades/netstandard.dll"']
    lines += [f'-r:"{json_dll}"', '"Assets/GearCraft/Scripts/RunSaveStore.cs"', f'"{qa}/StoreTests.cs.txt"']
    rsp = out/'tests.rsp'; rsp.write_text('\n'.join(lines))
    subprocess.run(compiler+['@'+str(rsp)], cwd=root, check=True)
    subprocess.run([str(unity/'MonoBleedingEdge/bin/mono'), str(out/'tests.exe')], cwd=root, check=True)

prefab = (root/'Assets/GearCraft/RunSaveRuntime.prefab').read_text()
blocks = {m[1]:m[2] for m in re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=\n--- !u!|\Z)', prefab, re.S)}
assert len(blocks) == len(re.findall(r'^--- !u!',prefab,re.M)), 'duplicate object IDs'
for line in prefab.splitlines():
    for fid in re.findall(r'\{fileID: (\d+)\}',line):
        assert fid == '0' or fid in blocks, ('missing local reference',fid)
for fid, body in blocks.items():
    if body.startswith('RectTransform:'):
        parent = re.search(r'm_Father: \{fileID: (\d+)\}',body)[1]
        if parent != '0':
            assert f'- {{fileID: {fid}}}' in blocks[parent], ('missing parent child link', fid)
catalog = re.findall(r'  - id: (\w+)\n    asset: \{fileID: 11400000, guid: (\w+), type: 2\}', prefab)
assert all(a == b for a,b in catalog)
asset_guids = {re.search(r'^guid: (\w+)',p.read_text(),re.M)[1] for d in ['WeaponData','UpgradeParts'] for p in (root/'Assets/GearCraft/ScriptableObject'/d).glob('*.asset.meta')}
assert {a for a,b in catalog} == asset_guids, 'catalog must include every weapon and part'
title = (root/'Assets/GearCraft/Scenes/Title.unity').read_text()
assert 'guid: 0544df54879d4dd48b046f8e8867ed53' in title
print(f'PASS: static prefab hierarchy/references; all {len(catalog)} weapons/parts catalogued; Title scene placement')
