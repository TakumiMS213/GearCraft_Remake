#!/usr/bin/env python3
"""Compose QA observer with unchanged native bot and build the six-case batch."""
from pathlib import Path
import subprocess
import sys

p = Path(__file__).resolve().parent
source = (p / "ReactiveGateSurvivalRoute.cs.txt").read_text()
observer = (p / "GeometryObserver.fragment.cs.txt").read_text()

def once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after)

source = once(source, "UnityEditor.EditorApplication.CallbackFunction tick=null;", observer + "\nUnityEditor.EditorApplication.CallbackFunction tick=null;")
source = once(source, 'attackSuppression="finished:"+reason+";previous="+attackSuppression;sampleDiagnostics(true);', 'geometryFlush();\n  attackSuppression="finished:"+reason+";previous="+attackSuppression;sampleDiagnostics(true);')
source = once(source, 'attackSuppression="frame_start";diagnosticTarget=null;', 'if(scene=="Main")observeGeometry(all);\n attackSuppression="frame_start";diagnosticTarget=null;')
(p / "RegressionSurvivalRoute.cs.txt").write_text(source)
subprocess.run([sys.executable, str(p / "build_batch.py"), "--route", "RegressionSurvivalRoute.cs.txt"], check=True)
