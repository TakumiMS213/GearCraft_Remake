#!/usr/bin/env python3
"""Compile QA snippets against current Editor DLLs; never runs or builds a game."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

here=Path(__file__).resolve().parent
root=here.parents[1]
version=re.search(r"^m_EditorVersion: (.+)$",(root/"ProjectSettings/ProjectVersion.txt").read_text(),re.M)[1]
contents=Path("/Applications/Unity/Hub/Editor")/version/"Unity.app/Contents"
refs=[contents/"MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll"]
refs+=sorted((contents/"Managed/UnityEngine").glob("*.dll"))
refs+=sorted((root/"Library/ScriptAssemblies").glob("*.dll"))
refs+=[root/"Assets/GearCraft/Plugins/Demigiant/DOTween/DOTween.dll"]
refs+=list((root/"Library/PackageCache").glob("com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll"))
names=["CaptureFresh","RestoreSession","NativeInputBootstrap","NativeInputSurvivalRoute","BatchDirect","BatchSurvival","LoadBatchDirect","LoadBatchSurvival"]
records=[]
for name in names:
    body=here/(name+".cs.txt")
    wrapper=here/(name+".compile.cs.txt")
    wrapper.write_text("using UnityEngine;\nusing UnityEditor;\nusing System;\nusing System.Linq;\nusing System.Collections.Generic;\npublic static class Probe_"+name+" { public static object Run() {\n"+body.read_text()+"\n} }\n")
    rsp=here/(name+".compile.rsp");out=here/(name+".compile.dll")
    rsp.write_text("\n".join(["-langversion:6","-target:library",'-out:"'+str(out)+'"']+['-r:"'+str(p)+'"' for p in refs]+['"'+str(wrapper)+'"'])+"\n")
    result=subprocess.run([str(contents/"MonoBleedingEdge/bin/mono"),str(contents/"MonoBleedingEdge/lib/mono/4.5/csc.exe"),"@"+str(rsp)],cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    record={"name":name,"exit_code":result.returncode,"sha256":hashlib.sha256(body.read_bytes()).hexdigest(),"compiler_output":result.stdout}
    records.append(record)
    print(name,result.returncode)
    if result.returncode: print(result.stdout)
    for generated in (wrapper,rsp,out):
        if generated.exists():
            generated.unlink()
(here/"compile-results.json").write_text(json.dumps(records,indent=2)+"\n")
raise SystemExit(int(any(r["exit_code"] for r in records)))
