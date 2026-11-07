"""Compile current production code and exercise the dorm tour without opening Unity or touching saves."""
from pathlib import Path
import subprocess
import tempfile
import re

root = Path(__file__).resolve().parents[2]
unity = Path('/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents')
compiler = [str(unity / 'NetCoreRuntime/dotnet'), str(unity / 'DotNetSdkRoslyn/csc.dll')]

with tempfile.TemporaryDirectory(prefix='gearcraft-dorm-check-') as directory:
    out = Path(directory)
    for label, artifact in [('editor', '200b0aE.dag'), ('player', '200b0aP.dag')]:
        response = (root / 'Library/Bee/artifacts' / artifact / 'Assembly-CSharp.rsp').read_text()
        response = re.sub(r'^-out:.*$', f'-out:"{out}/{label}.dll"', response, flags=re.M)
        response = re.sub(r'^-refout:.*$', f'-refout:"{out}/{label}.ref.dll"', response, flags=re.M)
        # The offline Editor response may still list the previously removed mailbox graphic.
        response = '\n'.join(line for line in response.splitlines() if not (
            line.startswith('"Assets/') and line.endswith('.cs"')
            and not (root / line.strip('"')).exists()))
        for source in (root / 'Assets/GearCraft/Scripts').rglob('*.cs'):
            name = source.relative_to(root).as_posix()
            if '/Editor/' not in name and '"' + name + '"' not in response:
                response += '\n"' + name + '"'
        rsp = out / (label + '.rsp')
        rsp.write_text(response)
        subprocess.run(compiler + ['@' + str(rsp)], cwd=root, check=True)
        print('PASS: ' + label + ' runtime compilation', flush=True)

    api = unity / 'MonoBleedingEdge/lib/mono/4.8-api'
    lines = ['-nologo', '-target:exe', '-nostdlib+', '-out:"' + str(out / 'tests.exe') + '"']
    lines += ['-r:"' + str(api / name) + '"' for name in ['mscorlib.dll', 'System.dll', 'System.Core.dll']]
    sources = ['CraftSpace/DormTaskProgress.cs', 'Story/ArrivalMessagePresenter.cs',
               'Story/ArrivalMessageModel.cs', 'Story/ArrivalMessageSequenceData.cs',
               'Story/StoryPlaybackRequest.cs']
    lines += ['"Assets/GearCraft/Scripts/' + name + '"' for name in sources]
    lines += ['"QA/DormTutorial20261101/PresenterTests.cs.txt"']
    rsp = out / 'tests.rsp'
    rsp.write_text('\n'.join(lines))
    subprocess.run(compiler + ['@' + str(rsp)], cwd=root, check=True)
    subprocess.run([str(unity / 'MonoBleedingEdge/bin/mono'), str(out / 'tests.exe')], cwd=root, check=True)
