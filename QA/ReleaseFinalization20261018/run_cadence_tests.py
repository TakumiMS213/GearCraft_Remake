#!/usr/bin/env python3
"""Compile and test the actual production pure scheduler without Unity/MCP state."""
from pathlib import Path
import hashlib
import json
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
qa = Path(__file__).resolve().parent
unity = Path('/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents')
dotnet = unity / 'NetCoreRuntime/dotnet'
refs = unity / 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
production = root / 'Assets/GearCraft/Scripts/Main/PlayerAttackCadence.cs'
probe = qa / 'PlayerAttackCadenceTests.cs.txt'
before = hashlib.sha256(production.read_bytes()).hexdigest()
with tempfile.TemporaryDirectory(prefix='gearcraft-cadence-', dir='/private/tmp') as tmp:
    tmp = Path(tmp)
    output = tmp / 'CadenceTests.dll'
    rsp = tmp / 'compile.rsp'
    args = ['-nologo', '-target:exe', '-langversion:8.0', '-out:"' + str(output) + '"']
    args += ['-r:"' + str(p) + '"' for p in sorted(refs.glob('*.dll'))]
    args += ['"' + str(production) + '"', '"' + str(probe) + '"']
    rsp.write_text('\n'.join(args), encoding='utf-8')
    built = subprocess.run([str(dotnet), str(unity / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)], text=True, capture_output=True)
    (qa / 'cadence-compile.txt').write_text(built.stdout + built.stderr, encoding='utf-8')
    if built.returncode:
        raise SystemExit(built.returncode)
    (tmp / 'CadenceTests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net6.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '6.0.21'}}}), encoding='utf-8')
    result = subprocess.run([str(dotnet), str(output)], text=True, capture_output=True, timeout=30)
    (qa / 'cadence-tests.txt').write_text(result.stdout + result.stderr, encoding='utf-8')
    after = hashlib.sha256(production.read_bytes()).hexdigest()
    meta = {'production_source': str(production.relative_to(root)), 'sha256_before': before, 'sha256_after': after, 'unchanged': before == after, 'compile_exit': built.returncode, 'test_exit': result.returncode, 'scope': 'Actual pure C# scheduler; no Unity, physical bullets, UI integration, MCP, or save state exercised.'}
    (qa / 'cadence-tests.json').write_text(json.dumps(meta, indent=2) + '\n', encoding='utf-8')
    print(result.stdout.splitlines()[0] if result.stdout else result.stderr)
    for line in result.stdout.splitlines():
        if line.startswith('FAIL,'): print(line)
    raise SystemExit(result.returncode)
