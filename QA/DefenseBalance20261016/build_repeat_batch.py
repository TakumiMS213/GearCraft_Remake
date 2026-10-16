#!/usr/bin/env python3
"""Prepare AR/EK seed repeats and a separate QA-only continuous-Play batch.

Twenty repeats change only seed/label. One additional seed77 EK GearCraft case
suppresses auto-jump against giant grounded bosses. Does not operate Unity or
write the primary/sensitivity runners, plans, manifests, templates or sources.
"""
import csv
import hashlib
import io
import json
from datetime import datetime, timezone
from pathlib import Path

from build_batch import CASE_FIELDS, digest, replace_once


FOLDER = Path(__file__).resolve().parent


def main():
    protected_names = [
        "BatchDefense.cs.txt", "batch-build-manifest.json",
        "BatchDefense.template.cs.txt", "NativeSurvivalBootstrap.cs.txt",
        "ReactiveGateSurvivalRoute.cs.txt", "batch-cases.csv",
        "progression-cases.csv", "execution-cases.json", "build_batch.py",
        "ReactiveGateSpeedSurvivalRoute.cs.txt", "BatchDefenseSensitivity.cs.txt",
        "sensitivity-batch-build-manifest.json", "sensitivity-cases.csv",
    ]
    protected = {name: (FOLDER / name).read_bytes() for name in protected_names}
    primary_reader = csv.DictReader(io.StringIO(protected["batch-cases.csv"].decode("utf-8")))
    assert primary_reader.fieldnames == CASE_FIELDS
    originals = [row for row in primary_reader if row["start_stage"] in ("28", "31")]
    assert len(originals) == 10
    assert {r["weapon"] for r in originals} == {"GearCraft", "SteamCannon", "SteamThrower", "RailCraft", "SteamGatling"}
    assert all(sum(r["start_stage"] == stage for r in originals) == 5 for stage in ("28", "31"))
    rows, links = [], []
    for seed in (101, 202):
        for source in originals:
            assert source["seed"] == "77" and source["label"].endswith("_seed77")
            row = dict(source)
            row["seed"] = str(seed)
            row["label"] = source["label"][:-len("_seed77")] + "_seed" + str(seed)
            assert all(row[k] == source[k] for k in CASE_FIELDS if k not in ("seed", "label"))
            rows.append(row)
            links.append(dict(label=row["label"], primary_label=source["label"], seed=seed))
    assert len({row["label"] for row in rows}) == 20
    grounded_sources = [r for r in originals if r["weapon"] == "GearCraft" and r["start_stage"] == "31"]
    assert len(grounded_sources) == 1
    grounded_source = grounded_sources[0]
    grounded_row = dict(grounded_source, label="gate_EK9_GearCraft_grounded_seed77")
    assert all(grounded_row[k] == grounded_source[k] for k in CASE_FIELDS if k != "label")
    rows.append(grounded_row)
    links.append(dict(label=grounded_row["label"], primary_label=grounded_source["label"], seed=77,
                      condition="Only giant ground-boss auto-jump suppressed; not a seed repeat"))
    case_path = FOLDER / "repeat-cases.csv"
    if case_path.exists():
        existing_reader = csv.DictReader(io.StringIO(case_path.read_bytes().decode("utf-8")))
        existing_rows = list(existing_reader)
        assert existing_reader.fieldnames == CASE_FIELDS
        if existing_rows == rows[:-1]:
            # Explicitly requested extra condition: retain the original 20 rows byte-for-byte.
            original_case_bytes = case_path.read_bytes()
            assert original_case_bytes.endswith(b"\n")
            with case_path.open("a", newline="") as stream:
                csv.DictWriter(stream, fieldnames=CASE_FIELDS, lineterminator="\n").writerow(grounded_row)
            assert case_path.read_bytes().startswith(original_case_bytes)
        else:
            assert existing_rows == rows, "Existing repeat plan differs; refusing overwrite"
    else:
        with case_path.open("x", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=CASE_FIELDS, lineterminator="\n")
            writer.writeheader()
            writer.writerows(rows)
    case_text = case_path.read_bytes().decode("utf-8")
    original_bootstrap = protected["NativeSurvivalBootstrap.cs.txt"].decode("utf-8")
    original_route = protected["ReactiveGateSurvivalRoute.cs.txt"].decode("utf-8")
    assert "reactive_gate_v1" in original_route and "reactiveBoostBudget = 2" in original_route
    assert "BOOST_budget=2" in original_route and "limited to two uses" in original_route
    bootstrap = replace_once(
        original_bootstrap,
        "UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;",
        "UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;\n"
        "bBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};")
    route = replace_once(original_route, "UnityEditor.EditorApplication.isPlaying=false;",
                         "Time.timeScale=0f; // Generated batch only: retain Play between cases.")
    route = replace_once(route, "System.Func<BonusCardDataSO,float> cardScore=data=>{",
                         "bRouteFinish=finish; // Generated cleanup hook; normal route semantics unchanged.\n"
                         "System.Func<BonusCardDataSO,float> cardScore=data=>{")
    jump_anchor = ' if(melee&&target!=null&&grounded&&Time.time>=nextJump&&bounds.center.y-player.transform.position.y>2.7f&&Mathf.Abs(dx)<2.5f){vy=player.jumpForce;nextJump=Time.time+.8f;}'
    jump_wrapper = ''' // Only the explicitly labeled posture probe suppresses auto-jump for giant grounded bosses.
 bool groundedProbeSuppressAutoJump=label.Contains("_grounded_")&&target!=null&&target.enemyData!=null&&
     (target.enemyData.aiType==EnemyAIType.Boss_Final||target.enemyData.aiType==EnemyAIType.Boss_Tank);
 if(!groundedProbeSuppressAutoJump){
''' + jump_anchor + '\n }'
    route = replace_once(route, jump_anchor, jump_wrapper)
    route = replace_once(route, 'System.Action<string,string> record=(eventName,details)=>{',
        'if(label.Contains("_grounded_"))System.IO.File.AppendAllText(folder+label+"-meta.txt","Posture sensitivity: only this labeled case suppresses melee auto-jump against Boss_Final/Boss_Tank. All other movement, native physics, attacks, defensive reactions, initial build, Gear0 and BOOST budget2 match the primary EK GearCraft seed77 case. This is not an additional seed replicate.\\n");\n'
        'System.Action<string,string> record=(eventName,details)=>{')
    route = replace_once(route, 'System.Action<EnemyController> updateReactive = enemyTarget => {',
        'if(label.Contains("_grounded_"))record("posture_condition","ground_boss_auto_jump_suppressed;Boss_Final/Boss_Tank only;primary EK GearCraft seed77 build and BOOST2 unchanged");\n'
        'System.Action<EnemyController> updateReactive = enemyTarget => {')
    assert route.count(jump_anchor) == 1, "Keep the ordinary jump condition verbatim inside the label-specific wrapper"
    assert "UnityEditor.EditorApplication.update-=tick;" in route
    output = protected["BatchDefense.template.cs.txt"].decode("utf-8")
    replacements = {
        "@@BOOTSTRAP@@": bootstrap, "@@ROUTE@@": route,
        "@@BOOTSTRAP_SHA@@": digest(original_bootstrap), "@@ROUTE_SHA@@": digest(original_route),
        "@@ROUTE_FILE@@": "ReactiveGateSurvivalRoute.cs.txt", "@@CASES_FILE@@": "repeat-cases.csv",
        "@@CASES_SHA@@": digest(case_text), "@@CSV_HEADER@@": ",".join(CASE_FIELDS),
    }
    for marker, value in replacements.items():
        assert marker in output, marker
        output = output.replace(marker, value)
    assert "@@" not in output
    output = replace_once(output,
        "// C#6 execute_code method body. GENERATED by build_batch.py; QA only.",
        "// C#6 execute_code method body. GENERATED by build_repeat_batch.py; independent AR/EK seed repeats; primary conditions retained; QA only.")
    route_anchor = '   bState=3;bPhaseAt=bNow;bRecord("route_invoked",bRunRoute());return;'
    guard = '''   var bRepeatMaterials=MaterialManager.Instance;
   if(bRepeatMaterials==null||bRepeatMaterials.GetMaterial(MaterialManager.MaterialType.Gear)!=0)
    throw new System.Exception("Primary seed repeat requires initial Gear0; no sensitivity carry permitted");
   if(bCurrent["start_stage"]!="28"&&bCurrent["start_stage"]!="31")throw new System.Exception("Repeat plan must contain AR/EK days only");
   bRecord("repeat_condition_validated","initialGear=0;BOOST budget2;primary profile unchanged;seed="+bCurrent["seed"]);
'''
    output = replace_once(output, route_anchor, guard + route_anchor)
    output = output.replace("rerun build_batch.py before execution", "rerun build_repeat_batch.py before execution")
    for name, before in protected.items():
        assert (FOLDER / name).read_bytes() == before, "Protected source changed during generation: " + name
    output_path = FOLDER / "BatchDefenseRepeat.cs.txt"
    output_path.write_text(output)
    manifest = dict(
        generated_utc=datetime.now(timezone.utc).isoformat(), output=output_path.name,
        output_sha256=digest(output), cases=case_path.name, case_count=len(rows), seed_repeat_count=20,
        seeds=[101, 202], posture_probe_seed=77,
        source_sha256={"NativeSurvivalBootstrap.cs.txt": digest(original_bootstrap),
                       "ReactiveGateSurvivalRoute.cs.txt": digest(original_route),
                       "BatchDefense.template.cs.txt": hashlib.sha256(protected["BatchDefense.template.cs.txt"]).hexdigest(),
                       "batch-cases.csv": hashlib.sha256(protected["batch-cases.csv"]).hexdigest(),
                       "repeat-cases.csv": digest(case_text)},
        protected_unchanged_sha256={name: hashlib.sha256(value).hexdigest() for name, value in protected.items()},
        condition_changes="First20: only seed/label suffix differ; last1: EK GearCraft seed77 build, suppress auto-jump only against Boss_Final/Tank. Gear0 / BOOST2 unchanged for all21",
        primary_case_links=links,
        transformations=["Native cleanup hooks and continuous-Play handoff identical to primary generator",
                         "Generated output only: validate initial Gear0 and log repeat_condition_validated before route",
                         "Generated route only: label.Contains(_grounded_) suppresses Boss_Final/Tank auto-jump; original normal jump condition retained verbatim",
                         "Grounded case only: append posture metadata and posture_condition event"],
        runtime_verification="Pending root Unity compile and execution; no MCP used",
        primary_manifest_handling="Main generator main() not called; primary runner and manifest were never written")
    (FOLDER / "repeat-batch-build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    for name, before in protected.items():
        assert (FOLDER / name).read_bytes() == before, "Protected file changed: " + name
    print(f"Generated {output_path.name}: {len(output.encode())} bytes, {len(rows)} cases; primary runner/manifest unchanged")


if __name__ == "__main__":
    main()
