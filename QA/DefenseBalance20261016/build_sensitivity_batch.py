#!/usr/bin/env python3
"""Generate only the independent IC sensitivity runner; never operate Unity.

Read the latest native bootstrap/template and the prepared six-BOOST route.
Keep the primary runner/manifest/template/plan byte-for-byte unchanged.
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
        "BatchDefenseRepeat.cs.txt", "repeat-batch-build-manifest.json",
        "repeat-cases.csv", "build_repeat_batch.py",
    ]
    protected = {name: (FOLDER / name).read_bytes() for name in protected_names}
    source_names = ["NativeSurvivalBootstrap.cs.txt", "ReactiveGateSpeedSurvivalRoute.cs.txt",
                    "BatchDefense.template.cs.txt", "sensitivity-cases.csv",
                    "sensitivity-extra-setup.csv"]
    # Match the generated C# File.ReadAllBytes hash, including CSV CRLF bytes.
    sources = {name: (FOLDER / name).read_bytes().decode("utf-8") for name in source_names}
    case_text = sources["sensitivity-cases.csv"]
    reader = csv.DictReader(io.StringIO(case_text))
    assert reader.fieldnames == CASE_FIELDS, "Sensitivity CSV must retain the main 14-column schema"
    cases = list(reader)
    assert len(cases) == 4 and [r["seed"] for r in cases] == ["77", "101", "202", "77"]
    assert cases[-1]["label"] == "gate_sensitivity_IC408_GearCraft_speed6_gear8_repair2_seed77"
    extras = list(csv.DictReader(io.StringIO(sources["sensitivity-extra-setup.csv"])))
    assert len(extras) == len(cases)
    for row, extra in zip(cases, extras):
        assert row["label"].startswith("gate_sensitivity_IC408_")
        assert row["label"] == extra["label"]
        assert extra["initial_gear"] == "8" and extra["boost_budget"] == "6"
        assert extra["route_file"] == "ReactiveGateSpeedSurvivalRoute.cs.txt"
        expected = dict(weapon="GearCraft", start_stage="7", profile="ic_speed_budget",
                        STR="4", ACC="4", moduleDamage="0", repair="0", barrier="0",
                        punk="1", scrap_module="1", initial_hp="100", initial_gate="100")
        if "_repair2_" in row["label"]:
            expected.update(STR="1", ACC="1", initial_gate="200")
            assert row is cases[-1] and "repair2_meals_forgone" in extra["condition"]
        assert all(row[key] == value for key, value in expected.items()), row
        assert all(not any(c in value for c in '\n\r,"/\\') for value in row.values())
    assert len({r["label"] for r in cases}) == len(cases)

    original_bootstrap = sources["NativeSurvivalBootstrap.cs.txt"]
    original_route = sources["ReactiveGateSpeedSurvivalRoute.cs.txt"]
    assert "reactive_gate_v1" in original_route and "reactiveBoostBudget = 6" in original_route
    assert "BOOST_budget=6" in original_route and "limited to six uses" in original_route
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
    route = replace_once(route, 'System.Action<string,string> record=(eventName,details)=>{',
        'if(label.Contains("_repair2_"))System.IO.File.AppendAllText(folder+label+"-meta.txt","Repair allocation sensitivity: two prior dorm repairs (+50 Gate each) instead of two meals; initial Gate200, STR1/ACC1, HP100, ic_speed_budget, Gear8 and BOOST budget6. This is a constructed arrival state and opportunity-cost comparison, not an observed-average full run. RepairModule remains disabled.\\n");\n'
        'System.Action<string,string> record=(eventName,details)=>{')
    assert "UnityEditor.EditorApplication.update-=tick;" in route
    output = sources["BatchDefense.template.cs.txt"]
    replacements = {
        "@@BOOTSTRAP@@": bootstrap, "@@ROUTE@@": route,
        "@@BOOTSTRAP_SHA@@": digest(original_bootstrap), "@@ROUTE_SHA@@": digest(original_route),
        "@@ROUTE_FILE@@": "ReactiveGateSpeedSurvivalRoute.cs.txt",
        "@@CASES_FILE@@": "sensitivity-cases.csv", "@@CASES_SHA@@": digest(case_text),
        "@@CSV_HEADER@@": ",".join(CASE_FIELDS),
    }
    for marker, value in replacements.items():
        assert marker in output, marker
        output = output.replace(marker, value)
    assert "@@" not in output
    output = replace_once(output,
        "// C#6 execute_code method body. GENERATED by build_batch.py; QA only.",
        "// C#6 execute_code method body. GENERATED by build_sensitivity_batch.py; independent IC affordability sensitivity; QA only.")
    punk_anchor = '   if(bRuntimePlayer.punkdrive!=null)bRuntimePlayer.punkdrive.SetActive(bRuntimeStatus.punkDrive);'
    output = replace_once(output, punk_anchor,
        '   // PunkDrive.Start caches PlayerController.STR; synchronize the row overrides before first activation.\n'
        '   bRuntimePlayer.STR=bRuntimeStatus.STR;bRuntimePlayer.ACC=bRuntimeStatus.ACC;\n' + punk_anchor)
    snapshot_anchor = '   var bSnapshot=typeof(StageFlowManager).GetMethod("CaptureDayStartSnapshot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);'
    carry_setup = '''   // Initial carry is prior-day inventory, not material gained during today's combat.
   var bSensitivityMaterials=MaterialManager.Instance;
   if(bSensitivityMaterials==null||Time.timeScale!=0f||bRuntimeFlow.IsStageActive||StageCounter.Instance==null||StageCounter.Instance.StageCount!=7)
    throw new System.Exception("Sensitivity initial carry requires paused pre-battle Main wave7");
   if(bCurrent["profile"]!="ic_speed_budget"||bCurrent["weapon"]!="GearCraft")
    throw new System.Exception("Unexpected sensitivity case setup");
   int bSensitivityGearBefore=bSensitivityMaterials.GetMaterial(MaterialManager.MaterialType.Gear);
   if(bSensitivityGearBefore!=0)throw new System.Exception("Expected cleared initial Gear; refusing duplicate sensitivity carry");
   bSensitivityMaterials.gear=8;
   bRecord("initial_carry","Gear "+bSensitivityGearBefore+" -> 8;prior-day budgeted carry;before snapshot;BOOST budget6;no combat replenishment");
'''
    output = replace_once(output, snapshot_anchor, carry_setup + snapshot_anchor)
    route_anchor = '   bState=3;bPhaseAt=bNow;bRecord("route_invoked",bRunRoute());return;'
    validate = '''   if(bSensitivityMaterials.GetMaterial(MaterialManager.MaterialType.Gear)!=8||bRuntimeStatus.HP!=int.Parse(bCurrent["initial_hp"])||
      Mathf.Abs(bRuntimeStatus.GATE-float.Parse(bCurrent["initial_gate"],System.Globalization.CultureInfo.InvariantCulture))>.001f||
      bRuntimeStatus.STR!=int.Parse(bCurrent["STR"])||bRuntimeStatus.ACC!=int.Parse(bCurrent["ACC"])||bRuntimeStatus.craftWeaponDamagebuff!=0||
      bRuntimePlayer.STR!=bRuntimeStatus.STR||bRuntimePlayer.ACC!=bRuntimeStatus.ACC||
      Mathf.Abs(bRuntimeStatus.bonusDamage-1f)>.0001f||Mathf.Abs(bRuntimeStatus.attackSpeedMult-.5527125f)>.00001f||
      bRuntimeStatus.module_repair||bRuntimeStatus.module_barrier||!bRuntimeStatus.punkDrive||!bRuntimeStatus.module_scrap)
    throw new System.Exception("Sensitivity initial stats/parts/materials failed validation before route");
   bRecord("sensitivity_validated","profile=ic_speed_budget;Gear=8;flat=1;speed=.5527125;HP="+bCurrent["initial_hp"]+";gate="+bCurrent["initial_gate"]+";STR="+bCurrent["STR"]+";ACC="+bCurrent["ACC"]+";module=0;BOOST budget6;player_stat_cache_synced_before_Punk_activation");
   System.IO.File.AppendAllText(bFolder+"native-survival-bootstrap-config-used.txt","case_override_STR="+bCurrent["STR"]+"\\ncase_override_ACC="+bCurrent["ACC"]+"\\ncase_override_GATE="+bCurrent["initial_gate"]+"\\ncase_override_initial_gear=8\\ncase_override_hit_damage="+bRuntimeStatus.GetWeaponDamage(bRuntimeStatus.currentWeapon).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\\n");
'''
    output = replace_once(output, route_anchor, validate + route_anchor)
    sidecar_guard_anchor = 'var bPlanLines=System.IO.File.ReadAllLines(bFolder+bPlanFile)'
    sidecar_guard = ('if(bHash(bFolder+"sensitivity-extra-setup.csv")!="' +
                     digest(sources["sensitivity-extra-setup.csv"]) +
                     '")throw new System.Exception("Sensitivity extra setup changed; regenerate the sensitivity runner");\n')
    output = replace_once(output, sidecar_guard_anchor, sidecar_guard + sidecar_guard_anchor)
    output = output.replace("rerun build_batch.py before execution", "rerun build_sensitivity_batch.py before execution")
    assert output.count("bSensitivityMaterials.gear=8;") == 1
    assert output.index("bSensitivityMaterials.gear=8;") < output.index(snapshot_anchor)
    assert output.index('bRecord("sensitivity_validated"') < output.index(route_anchor)
    for name, before in protected.items():
        assert (FOLDER / name).read_bytes() == before, "Protected source changed while building: " + name
    output_path = FOLDER / "BatchDefenseSensitivity.cs.txt"
    output_path.write_text(output)
    manifest = dict(
        generated_utc=datetime.now(timezone.utc).isoformat(), output=output_path.name,
        output_sha256=digest(output), cases="sensitivity-cases.csv", case_count=len(cases),
        source_sha256={name: digest(value) for name, value in sources.items()},
        protected_unchanged_sha256={name: hashlib.sha256(value).hexdigest() for name, value in protected.items()},
        transformations=["Same native cleanup hooks/continuous-Play handoff as main generator",
                         "Generated output only: set initial Gear0->8 before native day snapshot; log initial_carry",
                         "Generated output only: synchronize Player STR/ACC from case row before Punk first activation",
                         "Generated output only: validate row GATE/STR/ACC and Gear8/parts/flags before route; log sensitivity_validated and bootstrap case overrides",
                         "Repair2 condition only: append prior-night repair versus meal allocation metadata; no combat behavior changes",
                         "Generated output only: verify extra-setup sidecar hash"],
        runtime_verification="Pending root Unity compile and execution; no MCP used",
        primary_manifest_handling="Main generator main() was not called; shared manifest and primary runner were never written")
    (FOLDER / "sensitivity-batch-build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    for name, before in protected.items():
        assert (FOLDER / name).read_bytes() == before, "Protected file changed: " + name
    print(f"Generated {output_path.name}: {len(output.encode())} bytes, {len(cases)} cases; primary runner/manifest unchanged")


if __name__ == "__main__":
    main()
