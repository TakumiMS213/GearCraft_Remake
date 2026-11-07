"""Compile production sources and run isolated reset checks without touching real saves."""
from pathlib import Path
import subprocess, tempfile, re

root = Path(__file__).resolve().parents[2]
qa = Path(__file__).resolve().parent
unity = Path('/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents')
compiler = [str(unity/'NetCoreRuntime/dotnet'), str(unity/'DotNetSdkRoslyn/csc.dll')]

def compile_response(source, outdir, label):
    response = source.read_text()
    response = re.sub(r'^-out:.*$', f'-out:"{outdir}/{label}.dll"', response, flags=re.M)
    response = re.sub(r'^-refout:.*$', f'-refout:"{outdir}/{label}.ref.dll"', response, flags=re.M)
    for source in (root/'Assets/GearCraft/Scripts').rglob('*.cs'):
        relative = source.relative_to(root).as_posix()
        if '/Editor/' not in relative and '"'+relative+'"' not in response:
            response += '\n"'+relative+'"'
    rsp = outdir/(label+'.rsp'); rsp.write_text(response)
    subprocess.run(compiler+['@'+str(rsp)], cwd=root, check=True)

with tempfile.TemporaryDirectory(prefix='gearcraft-player-reset-') as directory:
    out = Path(directory)
    for label, artifact in [('editor','200b0aE.dag'), ('player','200b0aP.dag')]:
        compile_response(root/'Library/Bee/artifacts'/artifact/'Assembly-CSharp.rsp', out, label)
    api = unity/'MonoBleedingEdge/lib/mono/4.8-api'
    sources = ['Title/PlayerProgressReset.cs','Title/PlayerDataResetController.cs',
        'Title/StartingWeaponSettingsController.cs','Title/StartingWeaponProgress.cs',
        'Gallery/GalleryProgress.cs','Craft/CraftTutorialProgress.cs',
        'Craft/CraftRecipeUnlockModel.cs','Story/StoryPlaybackRequest.cs',
        'StatusManager.cs','CraftSpace/DormTaskProgress.cs','Data/MeleeUpgradeStats.cs','Data/UpgradeEffect.cs']
    lines = ['-nologo','-target:exe','-out:"'+str(out/'tests.exe')+'"','-nostdlib+']
    lines += ['-r:"'+str(api/name)+'"' for name in ['mscorlib.dll','System.dll','System.Core.dll']]
    lines += ['"Assets/GearCraft/Scripts/'+name+'"' for name in sources]
    lines += ['"'+str(qa/'ResetTests.cs.txt')+'"']
    rsp = out/'tests.rsp'; rsp.write_text('\n'.join(lines))
    subprocess.run(compiler+['@'+str(rsp)],cwd=root,check=True)
    subprocess.run([str(unity/'MonoBleedingEdge/bin/mono'),str(out/'tests.exe')],cwd=root,check=True)
