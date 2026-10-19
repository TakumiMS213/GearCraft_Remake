#!/usr/bin/env python3
"""Generate QA bodies; no Unity calls, source edits, or backup copying."""
from pathlib import Path
import csv
import hashlib
import io
import json

HERE = Path(__file__).resolve().parent
OLD = HERE.parent / "DefenseBugfix20261016"
FOLDER = "QA/ReleaseFinalization20261018/"


def once(text, old, new):
    if text.count(old) != 1:
        raise ValueError(f"Expected one anchor, found {text.count(old)}: {old[:100]}")
    return text.replace(old, new, 1)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def guard(folder, p):
    return f'''// Bind this run to a fresh backup captured in this Editor session.
foreach(string {p}Name in new[]{{"canonical-backup-ready.txt","typed-prefs-before.json","typed-prefs-before.txt","session-id.txt"}})if(!System.IO.File.Exists({folder}+{p}Name))throw new System.Exception("Run CanonicalCapture before Play: "+{p}Name);
string {p}Id=System.IO.File.ReadAllText({folder}+"session-id.txt").Trim();
if(UnityEditor.SessionState.GetString("GearCraft.QA.ReleaseFinalization.Session","")!={p}Id)throw new System.Exception("Fresh capture belongs to another Editor session; do not reuse stale backups");
if(System.IO.File.Exists({folder}+"canonical-restoration.txt"))throw new System.Exception("This session has already been restored; capture a new session before more tests");
var {p}Manifest=System.IO.File.ReadAllLines({folder}+"canonical-backup-ready.txt").Where(line=>line.Contains("=")).Select(line=>line.Split(new[]{{'='}},2)).ToDictionary(c=>c[0],c=>c[1]);
if({p}Manifest["session_id"]!={p}Id||{p}Manifest["keys"]!="16"||{p}Manifest["source"]!="current_session_native_PlayerPrefs")throw new System.Exception("Canonical session manifest mismatch");
foreach(var {p}Entry in {p}Manifest.Where(entry=>entry.Key.EndsWith(".txt")||entry.Key.EndsWith(".json"))){{
 using(var {p}Sha=System.Security.Cryptography.SHA256.Create()){{string {p}Actual=System.BitConverter.ToString({p}Sha.ComputeHash(System.IO.File.ReadAllBytes({folder}+{p}Entry.Key))).Replace("-","").ToLowerInvariant();if({p}Actual!={p}Entry.Value)throw new System.Exception("Session backup hash mismatch: "+{p}Entry.Key);}}
}}
var {p}Rows=System.IO.File.ReadAllLines({folder}+"typed-prefs-before.txt").Where(line=>!string.IsNullOrWhiteSpace(line)).Select(line=>line.Split('|')).ToArray();
if({p}Rows.Length!=16||{p}Rows.Any(c=>c.Length!=4)||{p}Rows.Select(c=>c[0]).Distinct().Count()!=16)throw new System.Exception("Canonical typed backup schema mismatch");
foreach(string {p}Required in new[]{{"GearCraft.Gallery.Progress.v1","GearCraft.Main.OpeningNotice.Seen.v1",CraftTutorialMetrics.CraftMasteryKey,CraftTutorialMetrics.WeaponMasteryKey,"GearCraft.CraftTutorial.Metrics.Craft.v5","GearCraft.CraftTutorial.Metrics.WeaponCustom.v5"}})if(!{p}Rows.Any(c=>c[0]=={p}Required))throw new System.Exception("Canonical key missing: "+{p}Required);
'''


def main():
    # Asset names / placement plans are read-only inputs. No old save/session data is copied.
    for name in ("loadouts.csv", "loadout-placements.csv"):
        target = HERE / name
        content = (OLD / name).read_bytes()
        if target.exists() and target.read_bytes() != content:
            raise ValueError("Do not overwrite changed input: " + name)
        target.write_bytes(content)

    bootstrap = (OLD / "NativeSurvivalBootstrap.cs.txt").read_text().replace(
        'const string folder="QA/DefenseBugfix20261016/";', f'const string folder="{FOLDER}";')
    start = bootstrap.index('string[] backup=')
    end = bootstrap.index('float beforeScale=', start)
    bootstrap = bootstrap[:start] + guard("folder", "bootSession") + bootstrap[end:]
    (HERE / "NativeInputBootstrap.cs.txt").write_text(bootstrap)

    route = (OLD / "RegressionSurvivalRoute.cs.txt").read_text().replace(
        'const string folder="QA/DefenseBugfix20261016/";', f'const string folder="{FOLDER}";')
    start = route.index('string[] prefRows=')
    end = route.index('int firstStage=', start)
    route = route[:start] + guard("folder", "routeSession") + route[end:]
    start = route.index('var coolTimeField=')
    end = route.index('var tooltipDataField=', start)
    route = route[:start] + '''var coolTimeField=typeof(PlayerController).GetField("coolTime",flags);
var processAttack=typeof(PlayerController).GetMethod("ProcessAttackInput",flags,null,new[]{typeof(bool),typeof(bool),typeof(float),typeof(Vector3)},null);
if(coolTimeField==null||processAttack==null||processAttack.ReturnType!=typeof(int))throw new System.Exception("Native ProcessAttackInput(bool,bool,float,Vector3)->int API required");
var getBulletSpeed=typeof(PlayerController).GetMethod("GetCurrentBulletSpeed",flags);
var sync=typeof(PlayerController).GetMethod("SyncWeaponFromStatus",flags);
var updateAnim=typeof(PlayerController).GetMethod("UpdateArmAnimation",flags);
var barrierUpdate=typeof(PlayerController).GetMethod("UpdateAutoBarrier",flags);
var poseMethod=typeof(ArmRotation).GetMethod("GetPoseIndex",flags);var offsetMethod=typeof(ArmRotation).GetMethod("GetOffset",flags);var scaleMethod=typeof(ArmRotation).GetMethod("GetScale",flags);
if(getBulletSpeed==null||sync==null||updateAnim==null||barrierUpdate==null||poseMethod==null||offsetMethod==null||scaleMethod==null)throw new System.Exception("Native maintenance/aim members missing");
int captureFps=System.IO.File.Exists(folder+"native-survival-fps.txt")?int.Parse(System.IO.File.ReadAllText(folder+"native-survival-fps.txt").Trim()):60;
if(!new[]{30,60,120}.Contains(captureFps))throw new System.Exception("Use a reviewed capture rate: 30,60,120");
''' + route[end:]
    meta_start = route.index('System.IO.File.WriteAllText(folder+label+"-meta.txt",')
    meta_end = route.index('\n', meta_start)
    meta = ("Native-input-v2: one native boss day from an injected initial build; not an acquisition playthrough or human success-rate estimate. "
            "PlayerController.Update/ArmRotation.Update are disabled for bot input. ProcessAttackInput(bool,bool,float,Vector3) is called exactly once per game frame, including false/false on blocked/no-target frames. "
            "No external cooldown assignment/decrement, PerformMeleeAttack call, projectile Instantiate, spread/double-shot/configure duplication. Returned int counts accepted native attack events. "
            "expected_projectiles is a branch-derived expectation, not observed Instantiate/hit count; melee=0, line=1, ordinary shot=1 or double=2. "
            "Native projectile/AI/physics/rewards/HP/gate/day-end remain active. No invulnerability, healing injection or direct enemy/projectile damage. "
            "Existing gate-aware-v1 bot movement/aim/click-repeat/0.2-second threat decisions/BOOST budget2 are retained for comparison; bot geometry and target limitations remain. "
            "Native auto-barrier is maintained. Aim applies previous muzzle/gravity correction through a world target passed to native shooting. "
            "Shot RNG is isolated as before; changing frame rate can still change bot decisions/physics and reward RNG. "
            "Use input CSV for FPS acceptance, not full-day kill-time equality. Fresh canonical 16-key backup is mandatory; root restores after Play. "
            "Native geometry observer now includes EnemyProjectileDamageable for BB heavy shots; first-seen is not synchronous spawn.")
    route = route[:meta_start] + 'System.IO.File.WriteAllText(folder+label+"-meta.txt",' + json.dumps(meta + "\n") + ');' + route[meta_end:]
    input_init = '''string inputPath=folder+label+"-native-input.csv";
var inputBuffer=new System.Text.StringBuilder();int nativeInputFrames=0;
System.IO.File.WriteAllText(inputPath,"game_seconds,frame,delta,capture_fps,pressed,held,accepted_attacks,expected_projectiles,cooldown_before,cooldown_after,weapon,aim_x,aim_y,request_reason\\n");
System.Action flushInput=()=>{if(inputBuffer.Length>0){System.IO.File.AppendAllText(inputPath,inputBuffer.ToString());inputBuffer.Length=0;}};
'''
    route = once(route, 'UnityEngine.Random.State aimRandom=UnityEngine.Random.state;\n', 'UnityEngine.Random.State aimRandom=UnityEngine.Random.state;\n' + input_init)
    route = once(route, '  geometryFlush();\n', '  geometryFlush();flushInput();\n')
    route = once(route, 'if(player!=null){coolTimeField.SetValue(player,oldCoolTime);player.enabled=oldPlayerEnabled;}', 'if(player!=null){player.enabled=oldPlayerEnabled;}')
    route = once(route, 'player.enabled=false;arm.enabled=false;coolTimeField.SetValue(player,0f);', 'player.enabled=false;arm.enabled=false; // Fresh player owns cadence state; never overwrite it.')
    route = once(route, 'Time.captureDeltaTime=1f/60f;', 'Time.captureDeltaTime=1f/captureFps;')
    route = once(route, 'tick=()=>{bool processCooldown=false;try{', 'tick=()=>{bool processCooldown=false,requestPressed=false,requestHeld=false;Vector3 requestAim=Vector3.zero;try{')
    route = once(route, ' if(!((float)coolTimeField.GetValue(player)<=0f)){attackSuppression="cooldown";return;}\n', '')
    route = once(route,
        ' if(melee){if(Vector2.Distance(player.transform.position,aimBounds.ClosestPoint(player.transform.position))>3.8f){attackSuppression="melee_out_of_range";return;}attackSuppression=intercept!=null?"melee_intercept_attempt":"melee_attack";coolTimeField.SetValue(player,weapon.coolTime*status.attackSpeedMult);damageField.SetValue(player,weapon.baseDamage);meleeAttack.Invoke(player,null);shots++;attackEvents++;return;}',
        ' if(melee){if(Vector2.Distance(player.transform.position,aimBounds.ClosestPoint(player.transform.position))>3.8f){attackSuppression="melee_out_of_range";return;}attackSuppression=intercept!=null?"melee_intercept_request":"melee_repeat_click_request";requestPressed=true;requestHeld=true;return;}')
    route = once(route, 'var projectileBody=weapon.bulletPrefab.GetComponent<Rigidbody2D>();', 'var projectileBody=weapon.bulletPrefab!=null?weapon.bulletPrefab.GetComponent<Rigidbody2D>():null;')
    start = route.index(' // Native PerformShotAttack parity,')
    end = route.index(' // BOT_SHARED_BLOCK_END', start)
    route = route[:start] + ''' // Native shooting reads this target, including the previous bot muzzle/gravity compensation.
 requestAim=origin+new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad),0f)*Mathf.Max(1f,aimOffset.magnitude);
 requestAim.z=0f;requestHeld=true;
''' + route[end:]
    start = route.index(' finally{\n  // Record once per second after the actual decision;')
    end = route.index('\n};\nUnityEditor.EditorApplication.update+=tick;', start)
    route = route[:start] + ''' finally{
  // Exactly one native admission/cadence advance for each observed Main game frame.
  if(processCooldown&&!done&&Application.isPlaying&&player!=null&&!player.enabled&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main"){
   try{
    float before=(float)coolTimeField.GetValue(player);int accepted;var shotWeapon=player.currentWeapon;
    var savedRandom=UnityEngine.Random.state;UnityEngine.Random.state=aimRandom;
    try{accepted=(int)processAttack.Invoke(player,new object[]{requestPressed,requestHeld,Time.deltaTime,requestAim});}
    finally{aimRandom=UnityEngine.Random.state;UnityEngine.Random.state=savedRandom;}
    if(accepted<0)throw new System.Exception("Native attack admission returned a negative count");
    int expected=shotWeapon!=null&&shotWeapon.weaponType==WeaponType.Ranged?accepted*(shotWeapon.useLineHitbox?1:status.hasBulletDouble?2:1):0;
    attackEvents+=accepted;shots+=expected;nativeInputFrames++;
    inputBuffer.AppendLine(string.Join(",",new[]{number(Time.time-started),Time.frameCount.ToString(),number(Time.deltaTime),captureFps.ToString(),requestPressed.ToString(),requestHeld.ToString(),accepted.ToString(),expected.ToString(),number(before),number((float)coolTimeField.GetValue(player)),csv(shotWeapon!=null?shotWeapon.weaponName:""),number(requestAim.x),number(requestAim.y),csv(attackSuppression)}));
    if(nativeInputFrames%captureFps==0)flushInput();
    sampleDiagnostics(false);
   }catch(System.Exception ex){System.IO.File.AppendAllText(folder+label+"-errors.txt",ex+"\\n");finish("failed_native_input_"+ex.Message);}
  }
 }''' + route[end:]
    route = route.replace('weapon,shots,spawned', 'weapon,expected_projectiles,spawned').replace('\\nshots=', '\\nexpected_projectiles=')
    # Read-only observer bug: the heavy prefab uses EnemyProjectileDamageable, not EnemyBulletController.
    route = once(route, 'FindObjectsByType<EnemyBulletController>(FindObjectsSortMode.None)){\n   if(projectile==null', 'FindObjectsByType<EnemyProjectileDamageable>(FindObjectsSortMode.None)){\n   if(projectile==null')
    route = once(route, 'number(projectile.bulletDamage)', 'number((float)typeof(EnemyProjectileDamageable).GetField("contactDamage",flags).GetValue(projectile))')
    for forbidden in ('coolTimeField.SetValue', 'coolTimeUpdate', 'PerformMeleeAttack', 'ConfigureSpawnedProjectile', 'Object.Instantiate(weapon.bulletPrefab'):
        # Metadata may mention the old names; actual reflection/direct use may not remain.
        body = '\n'.join(line for line in route.splitlines() if '-meta.txt' not in line)
        if forbidden in body:
            raise ValueError('Duplicated attack path remains: ' + forbidden)
    (HERE / 'NativeInputSurvivalRoute.cs.txt').write_text(route)

    fields = 'label weapon start_stage profile seed STR ACC moduleDamage repair barrier punk scrap_module initial_hp initial_gate fps'.split()
    cases_path = HERE / 'native-input-cases.csv'
    if not cases_path.exists():
        original = list(csv.DictReader((OLD / 'release2-cases.csv').open()))
        rows = [r for r in original if r['label'] in ('gate_release2_BB413_GearCraft_seed77', 'gate_release2_BB413_SteamThrower_seed77', 'gate_release2_EK9_GearCraft_seed77')]
        for row in rows:
            row['label'] = row['label'].replace('gate_release2_', 'gate_native_input_') + '_fps60'
            row['fps'] = '60'
        with cases_path.open('x', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=fields, lineterminator='\n'); writer.writeheader(); writer.writerows(rows)
    rows = list(csv.DictReader(cases_path.open()))
    if not rows or set(rows[0]) != set(fields) or len({r['label'] for r in rows}) != len(rows):
        raise ValueError('Invalid native input case plan')
    if any(r['fps'] not in ('30', '60', '120') for r in rows):
        raise ValueError('Unsupported planned FPS')
    template = (OLD / 'BatchDefense.template.cs.txt').read_text().replace('const string bFolder="QA/DefenseBugfix20261016/";', f'const string bFolder="{FOLDER}";')
    start = template.index('foreach(string backup in')
    end = template.index('if(!Application.isPlaying)', start)
    template = template[:start] + guard('bFolder', 'batchSession') + template[end:]
    template = template.replace('"NativeSurvivalBootstrap.cs.txt"', '"NativeInputBootstrap.cs.txt"')
    template = once(template, '   System.IO.File.WriteAllText(bFolder+"native-survival-name.txt",bCurrentLabel+"\\n");', '   System.IO.File.WriteAllText(bFolder+"native-survival-name.txt",bCurrentLabel+"\\n");\n   System.IO.File.WriteAllText(bFolder+"native-survival-fps.txt",bCurrent["fps"]+"\\n");')
    template = once(template, '   if(bRuntimeStatus.craftWeaponDamagebuff!=', '   if(bRuntimeStatus.craftWeaponDamagebuff!=')
    embedded_bootstrap = once(bootstrap, 'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;', 'UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;\nbBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};')
    embedded_route = once(route, 'UnityEditor.EditorApplication.isPlaying=false;', 'Time.timeScale=0f; // Batch retains Play between cases.')
    embedded_route = once(embedded_route, 'System.Func<BonusCardDataSO,float> cardScore=data=>{', 'bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{')
    substitutions = {
        '@@BOOTSTRAP@@': embedded_bootstrap, '@@ROUTE@@': embedded_route,
        '@@BOOTSTRAP_SHA@@': sha(bootstrap.encode()), '@@ROUTE_SHA@@': sha(route.encode()),
        '@@ROUTE_FILE@@': 'NativeInputSurvivalRoute.cs.txt', '@@CASES_FILE@@': cases_path.name,
        '@@CASES_SHA@@': sha(cases_path.read_bytes()), '@@CSV_HEADER@@': ','.join(fields)}
    for key,value in substitutions.items():
        template = template.replace(key,value)
    if '@@' in template: raise ValueError('Unresolved batch marker')
    (HERE / 'BatchNativeInput.cs.txt').write_text(template)
    loader = (OLD / 'LoadBatchDefense.cs.txt').read_text().replace('QA/DefenseBugfix20261016/BatchDefense.cs.txt', FOLDER+'BatchNativeInput.cs.txt')
    (HERE / 'LoadBatchNativeInput.cs.txt').write_text(loader)
    manifest = {'kind':'native-input-v2', 'cases':len(rows), 'outputs':{name:sha((HERE/name).read_bytes()) for name in ('NativeInputBootstrap.cs.txt','NativeInputSurvivalRoute.cs.txt','BatchNativeInput.cs.txt','native-input-cases.csv')}, 'source_read_only': {name:sha((OLD/name).read_bytes()) for name in ('RegressionSurvivalRoute.cs.txt','NativeSurvivalBootstrap.cs.txt','BatchDefense.template.cs.txt')}, 'runtime_verification':'pending root compile and execution', 'session_backup_copied':False}
    (HERE / 'native-input-build-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('Prepared native-input route/bootstrap/batch; cases='+str(len(rows))+'; no runtime/source/backup edits')


if __name__ == '__main__':
    main()
