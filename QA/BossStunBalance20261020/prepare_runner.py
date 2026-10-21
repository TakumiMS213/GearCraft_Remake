#!/usr/bin/env python3
"""Generate QA-only editor probes. Never executes Unity or restores saved data."""
from pathlib import Path
import csv
import hashlib
import io
import json
import re
from apply_driver_patch import patch_driver

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OLD = ROOT / "QA/ReleaseFinalization20261018"
POLISH = ROOT / "QA/ReleasePolish20261019"
DEFENSE = ROOT / "QA/DefenseBalance20261016"
FOLDER = "QA/BossStunBalance20261020/"
SESSION_KEY = "GearCraft.QA.BossStunBalance.Session"


def once(text, before, after):
    if text.count(before) != 1:
        raise ValueError(f"Expected one anchor ({text.count(before)}): {before[:100]}")
    return text.replace(before, after, 1)


def adapt(text):
    return text.replace("QA/ReleaseFinalization20261018/", FOLDER).replace(
        "QA/ReleasePolish20261019/", FOLDER).replace(
        "GearCraft.QA.ReleaseFinalization.Session", SESSION_KEY).replace(
        "GearCraft.QA.ReleasePolish.Session", SESSION_KEY)


def sha(content):
    return hashlib.sha256(content).hexdigest()


def put(name, text):
    (HERE / name).write_text(text)


def main():
    # No historical session data is copied.
    for name in ("loadouts.csv", "loadout-placements.csv"):
        put(name, (OLD / name).read_text())
    capture = adapt((POLISH / "CaptureFresh.cs.txt").read_text())
    capture = once(capture, '"audio-playerprefs-before.txt"})manifest.AppendLine',
                   '"audio-playerprefs-before.txt","time-ticks-before.txt","dotween-before.txt"})manifest.AppendLine')
    put("CaptureFresh.cs.txt", capture)
    restore = adapt((POLISH / "RestoreCanonicalSession.cs.txt").read_text())
    restore = re.sub(r'const string expectedSession="[^"]+";',
                     'string expectedSession=System.IO.File.ReadAllText(folder+"session-id.txt").Trim();', restore)
    restore = re.sub(r'^if\(hash\(folder\+"time-ticks-before\.txt"\).*$', '', restore, flags=re.M)
    restore = restore.replace('text=="ReleaseFinalizationQA480"||text.StartsWith("ReleaseFinalizationQA480 (")',
                              'text=="BossStunQA480"||text.StartsWith("BossStunQA480 (")')
    put("RestoreSession.cs.txt", restore)

    bootstrap = adapt((OLD / "NativeInputBootstrap.cs.txt").read_text())
    put("NativeInputBootstrap.cs.txt", bootstrap)
    route = adapt((OLD / "NativeInputSurvivalRoute.cs.txt").read_text())
    route = once(route, 'if(now-wallStarted>=300){finish("wall_timeout_300s");return;}',
                 'if(now-wallStarted>=900){finish("wall_timeout_900s");return;}')
    # Ground bosses remain approachable without repeatedly jumping toward their tall center.
    route = once(route, 'if(melee&&target!=null&&grounded&&Time.time>=nextJump',
                 'if(melee&&target!=null&&target.GetComponent<EnemyAI_BossTank>()==null&&target.GetComponent<EnemyAI_BossFinal>()==null&&grounded&&Time.time>=nextJump')
    # Straight projectile prediction uses expanded bounds first contact, matching gate prediction.
    anchor = '  float closestTime=velocity.sqrMagnitude>0.01f?-Vector2.Dot(relative,velocity)/velocity.sqrMagnitude:float.MaxValue;'
    route = once(route, anchor, anchor + '''
  if(!bomb){
   Vector2 contactExtents=(Vector2)ownBounds.extents+(Vector2)collider.bounds.extents+Vector2.one*0.25f;
   float contactEnter=0f,contactExit=1.2f;bool contactIntersects=true;
   for(int axis=0;axis<2;axis++){
    float pos=axis==0?relative.x:relative.y,spd=axis==0?velocity.x:velocity.y,ext=axis==0?contactExtents.x:contactExtents.y;
    if(Mathf.Abs(spd)<0.001f){if(Mathf.Abs(pos)>ext){contactIntersects=false;break;}}
    else{float first=(-ext-pos)/spd,last=(ext-pos)/spd;contactEnter=Mathf.Max(contactEnter,Mathf.Min(first,last));contactExit=Mathf.Min(contactExit,Mathf.Max(first,last));if(contactEnter>contactExit){contactIntersects=false;break;}}
   }
   closestTime=contactIntersects&&contactExit>=0f?contactEnter:float.MaxValue;
  }''')
    route = once(route, 'string progress=folder+"native-survival-progress.txt";', '''string progress=folder+"native-survival-progress.txt";
string runMode=System.IO.File.Exists(folder+"run-mode.txt")?System.IO.File.ReadAllText(folder+"run-mode.txt").Trim():"survival";
if(runMode!="survival"&&runMode!="direct")throw new System.Exception("Expected run-mode survival or direct");
bool directMode=runMode=="direct",directPrepared=false;EnemyController directBoss=null;int directBossId=0;float directSpawnTime=-1f;
''')
    observer = (HERE / "StunObserver.fragment.cs.txt").read_text()
    route = once(route, 'UnityEditor.EditorApplication.CallbackFunction tick=null;', observer + '\nUnityEditor.EditorApplication.CallbackFunction tick=null;')
    route = once(route, '  geometryFlush();flushInput();', '  observeStuns(new EnemyController[0]);flushStuns();geometryFlush();flushInput();')
    route = once(route, ' if(scene=="Main")observeGeometry(all);', ' if(scene=="Main"){observeGeometry(all);observeStuns(all);}')
    route = once(route, ' int stage=StageCounter.Instance!=null?StageCounter.Instance.StageCount:lastStage;', '''
 if(directMode&&directPrepared&&closedBosses.Contains(directBossId)){
  bool defeated=bossLastHP.ContainsKey(directBossId)&&bossLastHP[directBossId]<=0f;
  finish(defeated?"direct_boss_defeated":"direct_boss_unobserved_despawn");return;
 }
 int stage=StageCounter.Instance!=null?StageCounter.Instance.StageCount:lastStage;''')
    # Await the native day introduction to finish before replacing its first spawn group.
    # Boss AI/physics/projectiles, gate defeat and player HP remain native.
    route = once(route, ' var alive=all.Where(e=>e.HP>0f', '''
 if(directMode&&!directPrepared){
  flow.enemySpawner.StopSpawning();
  foreach(var prior in all){prior.isLastEnemy=false;prior.gameObject.SetActive(false);UnityEngine.Object.Destroy(prior.gameObject);}
  var active=(System.Collections.Generic.List<EnemyController>)typeof(StageFlowManager).GetField("activeEnemies",flags).GetValue(flow);active.Clear();
  string bossAsset=firstStage==7?"lv3_IC-408":firstStage==16?"lv3_BB-413":firstStage==28?"lv3_AR-227":firstStage==31?"lv3_FinalBoss":null;
  if(bossAsset==null)throw new System.Exception("No direct boss for initial stage");
  var bossData=UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/GearCraft/ScriptableObject/EnemyData/"+bossAsset+".asset");
  var instance=UnityEngine.Object.Instantiate(bossData.prefab,flow.enemySpawner.transform.position,Quaternion.identity);
  directBoss=instance.GetComponent<EnemyController>();if(directBoss==null)throw new System.Exception("Direct boss missing native EnemyController");
  directBoss.enemyData=bossData;directBoss.isLastEnemy=false;directSpawnTime=Time.time;directBossId=directBoss.GetInstanceID();directPrepared=true;
  // Two real BOOST charges model carried resources; this direct condition is distinct from day-survival starting at Gear0.
  MaterialManager.Instance.AddMaterial(MaterialManager.MaterialType.Gear,2);
  record("direct_boss_spawn_requested","asset="+bossAsset+";native day scaling="+flow.EnemyScalingStage+";no HP/GATE replenishment;Gear2 provisioned");
  return;
 }
 var alive=all.Where(e=>e.HP>0f''')
    route = once(route, 'bossStarts[id]=new[]{Time.time-started,', 'bossStarts[id]=new[]{directMode&&id==directBossId?directSpawnTime-started:Time.time-started,')
    route = once(route, 'Native-input-v2: one native boss day', 'Native-input-v3: mode in run-mode.txt; direct replaces first spawn group with native boss (same day scaling, Gear2 carried); survival preserves full day. No HP/GATE replenishment. Ground-boss autoJump disabled; projectile threats use bounds entry. One native boss day')
    route = once(route, '"result="+reason+"\\nstart_hp="', '"result="+reason+"\\nmode="+runMode+"\\nstart_hp="')
    route = patch_driver(route)
    put("NativeInputSurvivalRoute.cs.txt", route)

    source_rows = list(csv.DictReader((DEFENSE / "batch-cases.csv").open()))
    fields = list(source_rows[0]) + ["fps"]
    for mode in ("direct", "survival"):
        rows = []
        for row in source_rows:
            row = dict(row)
            row["label"] = row["label"].replace("gate_", "stun_"+mode+"_")+"_pass1"
            row["fps"] = "60"
            rows.append(row)
        buf=io.StringIO(); writer=csv.DictWriter(buf,fieldnames=fields,lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
        put("all-"+mode+"-cases.csv", buf.getvalue())
        if mode=="direct":
            selected={("7","GearCraft"),("16","SteamCannon"),("28","SteamGatling"),("31","SteamGatling")}
            rows=[dict(r,label=r["label"].replace("_pass1","_calibration1")) for r in rows if (r["start_stage"],r["weapon"]) in selected]
            buf=io.StringIO();writer=csv.DictWriter(buf,fieldnames=fields,lineterminator="\n")
            writer.writeheader();writer.writerows(rows)
        put(mode+"-cases.csv", buf.getvalue())
        # Preserve the reviewed batch teardown/title settling; replace only embedded bodies and plan.
        batch = adapt((OLD / "BatchNativeInput.cs.txt").read_text())
        start = batch.index('System.Func<string> bRunBootstrap=()=>{')
        end = batch.index('double bStartedAt=', start)
        boot = once(bootstrap, 'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;',
                    'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;\nbBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};')
        body = once(route, 'UnityEditor.EditorApplication.isPlaying=false;', 'Time.timeScale=0f; // Batch keeps Play until all cases finish.')
        body = once(body, 'System.Func<BonusCardDataSO,float> cardScore=data=>{', 'bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{')
        batch=batch[:start]+'System.Func<string> bRunBootstrap=()=>{\n'+boot+'\n};\nSystem.Func<string> bRunRoute=()=>{\n'+body+'\n};\n'+batch[end:]
        batch=batch.replace('const string bPlanFile="native-input-cases.csv";', 'const string bPlanFile="'+mode+'-cases.csv";')
        batch=re.sub(r'if\(bHash\(bFolder\+"NativeInputBootstrap.cs.txt"\).*?throw new System.Exception\("Batch source/plan changed; rerun build_batch.py before execution"\);',
          'if(bHash(bFolder+"NativeInputBootstrap.cs.txt")!="'+sha(bootstrap.encode())+'"||bHash(bFolder+"NativeInputSurvivalRoute.cs.txt")!="'+sha(route.encode())+'"||bHash(bFolder+bPlanFile)!="'+sha(buf.getvalue().encode())+'")throw new System.Exception("QA sources/plan changed; regenerate before execution");', batch)
        batch=once(batch, '   bRecord("case_begin",bExpectedConfig);', '   System.IO.File.WriteAllText(bFolder+"run-mode.txt","'+mode+'\\n");\n   bRecord("case_begin",bExpectedConfig);')
        batch=batch.replace('60+bPending.Count*380', '120+bPending.Count*1000').replace('bNow-bPhaseAt>340','bNow-bPhaseAt>950')
        batch=once(batch, 'bool bExpectedTerminal=bReason=="player_dead"', 'bool bExpectedTerminal=bReason=="direct_boss_defeated"||bReason=="direct_boss_unobserved_despawn"||bReason=="wall_timeout_900s"||bReason=="player_dead"')
        put("Batch"+mode.title()+".cs.txt", batch)
        loader=adapt((OLD/"LoadBatchNativeInput.cs.txt").read_text()).replace("BatchNativeInput.cs.txt","Batch"+mode.title()+".cs.txt")
        put("LoadBatch"+mode.title()+".cs.txt",loader)
    put("prepared-manifest.json",json.dumps({"prepared_only":True,"runtime_executed":False,
        "session_backup_copied":False,"direct_calibration_cases":4,"all_cases_per_mode":14,
        "files":{p.name:sha(p.read_bytes()) for p in HERE.glob("*.cs.txt")},
        "source_readonly":[str(OLD),str(POLISH),str(DEFENSE)]},indent=2)+"\n")
    print("Prepared fresh-session scripts, 4 direct calibration + all 14 direct/day-survival plans. Nothing executed in Unity.")


if __name__ == "__main__":
    main()
