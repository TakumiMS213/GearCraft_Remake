#!/usr/bin/env python3
"""Compile the three changed AI files against existing Unity/game assemblies.

This does not run Unity, import the output DLL, or validate runtime behavior.
Run from the project root: python3 QA/DefenseBugfix20261016/compile_changed_ai.py
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

folder = Path(__file__).resolve().parent
root = folder.parents[1]
version = re.search(r"^m_EditorVersion: (.+)$", (root / "ProjectSettings/ProjectVersion.txt").read_text(), re.M)[1]
contents = Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents"
mono = contents / "MonoBleedingEdge/bin/mono"
csc = contents / "MonoBleedingEdge/lib/mono/4.5/csc.exe"
sources = [root / "Assets/GearCraft/Scripts/EnemyAI" / (name + ".cs") for name in (
    "EnemyAI_BossSpeed", "EnemyAI_BossArtillery", "EnemyAI_BossFinal")]
references = [contents / "MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll"]
references += sorted((contents / "Managed/UnityEngine").glob("*.dll"))
references += sorted((root / "Library/ScriptAssemblies").glob("*.dll"))
references += [root / "Assets/GearCraft/Plugins/Demigiant/DOTween/DOTween.dll"]
missing = [str(p) for p in [mono, csc, *sources, *references] if not p.is_file()]
if missing:
    raise SystemExit("Required local compiler/source/reference missing: " + ", ".join(missing))

def hashes():
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}

before = hashes()
rsp = folder / "changed-ai.compile.rsp"
output = folder / "changed-ai.compile.dll"
lines = ["-langversion:latest", "-target:library", '-out:"' + str(output) + '"']
lines += ['-r:"' + str(p) + '"' for p in references]
lines += ['"' + str(p) + '"' for p in sources]
rsp.write_text("\n".join(lines) + "\n")
command = [str(mono), str(csc), "@" + str(rsp)]
result = subprocess.run(command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
after = hashes()
(folder / "changed-ai.compile.log.txt").write_text(result.stdout)
record = {
    "purpose": "Syntax/API check only; output is outside Assets and is not loaded into Unity.",
    "command": command,
    "compiler_exit_code": result.returncode,
    "source_unchanged_during_compile": before == after,
    "source_sha256": before,
    "output_sha256": hashlib.sha256(output.read_bytes()).hexdigest() if result.returncode == 0 else None,
}
(folder / "changed-ai.compile.json").write_text(json.dumps(record, indent=2) + "\n")
print(result.stdout, end="")
if before != after:
    print("Source changed during compilation; rerun after edits finish.", file=sys.stderr)
sys.exit(result.returncode or (0 if before == after else 2))
