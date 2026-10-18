"""Generate independent C#6 MCP bodies. Preparation only; never executes Unity."""
from pathlib import Path
import json

folder = Path(__file__).resolve().parent
previous = folder.parent / 'DefenseBugfix20261016'
backup = 'QA/DefenseBugfix20261016/'
dest = 'QA/ReleaseReadiness20261016/'

bootstrap = (previous / 'NativeSurvivalBootstrap.cs.txt').read_text()
bootstrap = bootstrap.replace('const string folder="' + backup + '";', 'const string folder="' + dest + '";\nconst string backupFolder="' + backup + '";\nif(!System.IO.File.Exists(backupFolder+"session-backup-ready.txt"))throw new System.Exception("Fresh session backup required");\nif(System.IO.File.Exists(folder+"probe-progress.txt")&&System.IO.File.ReadAllText(folder+"probe-progress.txt").StartsWith("RUNNING"))throw new System.Exception("A local probe is running");')
bootstrap = bootstrap.replace('ReadAllLines(folder+"playerprefs-before.txt")', 'ReadAllLines(backupFolder+"playerprefs-before.txt")')
(folder / 'PreparePausedMain.cs.txt').write_text(bootstrap)
(folder / 'native-survival-config.csv').write_text('weapon,startStage,profile,seed\nGearCraft,4,none,77\n')
(folder / 'loadouts.csv').write_text('profile,parts,moduleDamage\nnone,,0\n')
(folder / 'loadout-placements.csv').write_text('profile,part,gridX,gridY,rotation\n')

common = r'''// C#6 execute_code method body. Starts only from this QA's READY paused Main.
const string folder="QA/ReleaseReadiness20261016/";
const string backupFolder="QA/DefenseBugfix20261016/";
const string label="__LABEL__";
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Main"||Time.timeScale!=0f)throw new System.Exception("Requires READY paused Main");
if(!System.IO.File.Exists(backupFolder+"session-backup-ready.txt")||System.IO.File.ReadAllLines(backupFolder+"playerprefs-before.txt").Count(x=>!string.IsNullOrWhiteSpace(x))!=8)throw new System.Exception("Fresh eight-key backup required");
if(!System.IO.File.Exists(folder+"native-survival-bootstrap-progress.txt")||!System.IO.File.ReadAllText(folder+"native-survival-bootstrap-progress.txt").StartsWith("READY"))throw new System.Exception("PreparePausedMain must finish first");
if(System.IO.File.Exists(folder+label+"-result.txt"))throw new System.Exception("Existing result: archive it explicitly before repeating");
if(System.IO.File.Exists(folder+"probe-progress.txt")&&System.IO.File.ReadAllText(folder+"probe-progress.txt").StartsWith("RUNNING"))throw new System.Exception("Another local probe is running");
if(MainDebugEnemySpawnWindow.IsDebugModeActive||EndingManager.IsLoadingEnding)throw new System.Exception("Debug/Ending must be inactive");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var status=StatusManager.Instance;var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var flow=StageFlowManager.Instance;var gate=GateManager.Instance;
if(status==null||player==null||flow==null||gate==null||player.runtimeStatus!=status)throw new System.Exception("Native Main managers not ready");
var initialScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
double wallStart=UnityEditor.EditorApplication.timeSinceStartup;
float captureBefore=Time.captureDeltaTime;
var evidence=new System.Text.StringBuilder();
System.Action<string> note=(s)=>evidence.AppendLine(System.DateTime.UtcNow.ToString("O")+" "+s);
System.Action<bool,string> require=(ok,s)=>{if(!ok)throw new System.Exception(s);};
UnityEditor.EditorApplication.CallbackFunction tick=null;
bool done=false;
System.Action<string> finish=(outcome)=>{
 if(done)return;done=true;
 if(tick!=null)UnityEditor.EditorApplication.update-=tick;
 Time.captureDeltaTime=captureBefore;
 note(outcome);
 System.IO.File.WriteAllText(folder+label+"-result.txt",outcome+"\n"+evidence.ToString()+"\nPlay stopped; fresh PlayerPrefs/editor restoration is still required.\n");
 System.IO.File.WriteAllText(folder+"probe-progress.txt",outcome+" "+label+"\n");
 UnityEditor.EditorApplication.isPlaying=false;
};
System.IO.File.WriteAllText(folder+"probe-progress.txt","RUNNING "+label+"\n");
note("begin scene_handle="+initialScene.handle+" HP="+status.HP+" GATE="+status.GATE+" speed="+status.attackSpeedMult);
Time.captureDeltaTime=1f/60f;
if(flow.enemySpawner!=null)flow.enemySpawner.StopSpawning();
'''

gate = r'''
try{
 var hit=typeof(StageFlowManager).GetField("gateWasHit",flags);
 var inv=typeof(GateManager).GetField("isInvincible",flags);
 require(hit!=null&&inv!=null,"Gate/Flow fields missing");
 status.GATE=100;status.killAllEnemies=true;hit.SetValue(flow,false);inv.SetValue(gate,false);
 gate.TakeDamage(7f);
 require(Mathf.Approximately(status.GATE,93f),"First damage was not exactly seven");
 require((bool)hit.GetValue(flow)&&!status.killAllEnemies,"Actual gate damage did not revoke PERFECT eligibility");
 require((bool)inv.GetValue(gate),"Invincibility did not start synchronously");
 note("first accepted: GATE=93, gateWasHit=true, killAllEnemies=false, invincible=true");
 // Reset only the notification sentinels so a duplicate notification can be detected.
 // This deliberately does not simulate a complete wave or assert the rendered PERFECT popup.
 hit.SetValue(flow,false);status.killAllEnemies=true;
 gate.TakeDamage(7f);
 require(Mathf.Approximately(status.GATE,93f),"Invincible gate accepted another hit");
 require(!(bool)hit.GetValue(flow)&&status.killAllEnemies,"Invincible hit notified Flow again");
 note("second rejected: GATE=93, notification sentinels unchanged");
 finish("PASS");
}catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
return "Gate damage probe finished; inspect "+label+"-result.txt";
'''

boost = r'''
try{
 var boost=typeof(PlayerController).GetMethod("BoostActive",flags);
 var transform=typeof(PlayerController).GetMethod("ChangeGearCraft",flags);
 var active=typeof(PlayerController).GetField("isBoostActive",flags);
 var remaining=typeof(PlayerController).GetField("boostRemainingTime",flags);
 require(boost!=null&&transform!=null&&active!=null&&remaining!=null,"BOOST/transform reflection members missing");
 require(player.currentWeapon!=null&&player.currentWeapon.weaponName=="GearCraft_Sword","GearCraft Sword is required");
 MaterialManager.Instance.AddMaterial(MaterialManager.MaterialType.Gear,2);
 status.attackSpeedMult=0.8f;
 boost.Invoke(player,null);transform.Invoke(player,null);
 require((bool)active.GetValue(player),"BOOST did not activate");
 require(status.currentWeapon!=null&&status.currentWeapon.weaponName=="GearCraft_Axe","Gear transform did not activate");
 float suspendedSpeed=status.attackSpeedMult;
 float suspendedRemaining=(float)remaining.GetValue(player);
 var suspendedWeapon=status.currentWeapon;
 player.enabled=false;Time.timeScale=0f;
 note("Gallery-equivalent suspension: enabled=false, speed="+suspendedSpeed+", remaining="+suspendedRemaining+", weapon="+suspendedWeapon.weaponName);
 int phase=0;float phaseGame=Time.time;double phaseWall=UnityEditor.EditorApplication.timeSinceStartup;WeaponDataSO nextWeapon=null;int stableSamples=0;
 // Keep the isolated empty Main from starting its wave after the opening display finishes.
 typeof(StageFlowManager).GetField("dayRetryTriggered",flags).SetValue(flow,true);
 tick=()=>{
  try{
   if(!Application.isPlaying){finish("ABORTED");return;}
   require(UnityEditor.EditorApplication.timeSinceStartup-wallStart<40,"Probe wall timeout");
   require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==initialScene.handle,"Unexpected scene transition");
   if(phase==0){
    require(player!=null&&!(player.enabled)&&(bool)active.GetValue(player),"Disabled Player lost BOOST");
    require(Mathf.Approximately(status.attackSpeedMult,suspendedSpeed)&&Mathf.Approximately((float)remaining.GetValue(player),suspendedRemaining)&&status.currentWeapon==suspendedWeapon,"Paused suspension changed temporary combat state");
    if(UnityEditor.EditorApplication.timeSinceStartup-phaseWall<0.7)return;
    player.enabled=true;Time.timeScale=1f;phaseGame=Time.time;phase=1;note("suspension retained state; resumed enabled=true/timeScale1");return;
   }
   if(phase==1){
    if(Time.time-phaseGame<0.35f)return;
    require((float)remaining.GetValue(player)<suspendedRemaining,"BOOST did not resume elapsed-time progression");
    require(status.currentWeapon==suspendedWeapon,"Transform expired before its natural duration");
    note("resumed remaining="+remaining.GetValue(player)+"; destroying old Player");
    UnityEngine.Object.Destroy(player.gameObject);phase=2;return;
   }
   if(phase==2){
    if(player!=null)return;
    status.ResetRunProgress();status.attackSpeedMult=0.73f;nextWeapon=status.currentWeapon;phaseGame=Time.time;phase=3;
    note("old Player destroyed; new run sentinel speed=.73 weapon="+(nextWeapon==null?"null":nextWeapon.weaponName));return;
   }
   require(Mathf.Approximately(status.attackSpeedMult,0.73f),"Old BOOST overwrote the new run speed");
   require(status.currentWeapon==nextWeapon,"Old Gear transform overwrote the new run weapon");
   require(!status.steamThrowerBoostBurnDropsActive,"Old BOOST burn state leaked");
   stableSamples++;
   if(Time.time-phaseGame>=6f){note("new state stable for "+(Time.time-phaseGame)+" game seconds; samples="+stableSamples);finish("PASS");}
  }catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
 };
 UnityEditor.EditorApplication.update+=tick;
}catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
return "Scheduled BOOST/suspension/lifetime probe; inspect probe-progress.txt";
'''

death = r'''
try{
 var inv=typeof(PlayerController).GetField("isInvincible",flags);
 var gateInv=typeof(GateManager).GetField("isInvincible",flags);
 var retry=typeof(StageFlowManager).GetField("dayRetryTriggered",flags);
 require(inv!=null&&gateInv!=null&&retry!=null,"Death/Flow fields missing");
 inv.SetValue(player,false);gateInv.SetValue(gate,false);status.module_barrier=false;status.HP=1;status.GATE=1;
 var transition=UnityEngine.Object.FindFirstObjectByType<SceneTransitionManager>();
 require(transition!=null,"Native SceneTransitionManager required");
 // Hold the native transition longer than the player's one-second retry delay.
 // Only runtime timing is changed. A real Ending scene is loaded by the native methods.
 transition.fadeDuration=0.05f;
 typeof(SceneTransitionManager).GetField("minLoadingSeconds",flags).SetValue(transition,2f);
 typeof(SceneTransitionManager).GetField("maxLoadingSeconds",flags).SetValue(transition,2f);
 Time.timeScale=1f;
 __DAMAGE_ORDER__
 require(EndingManager.IsLoadingEnding&&EndingManager.PendingEndingNum==1,"Gate destruction did not request BAD Ending");
 note("same-call damage order=__ORDER_LABEL__; HP="+status.HP+" GATE="+status.GATE+" EndingPending=1");
 int pendingSamples=0,endingSamples=0;float endingGame=0f;
 tick=()=>{
  try{
   if(!Application.isPlaying){finish("ABORTED");return;}
   require(UnityEditor.EditorApplication.timeSinceStartup-wallStart<30,"Probe wall timeout");
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   if(scene.handle==initialScene.handle){
    require(!(bool)retry.GetValue(flow),"Player retry was triggered while Ending was loading");
    require(EndingManager.IsLoadingEnding,"Ending lost its loading guard before scene change");
    pendingSamples++;return;
   }
   require(scene.name=="Ending","Unexpected scene instead of Ending: "+scene.name);
   require(EndingManager.PendingEndingNum==1,"Wrong ending number");
   require(Time.timeScale==1f,"Ending timeScale not resumed");
   if(endingSamples++==0){endingGame=Time.time;note("native Ending loaded; pending samples="+pendingSamples);}
   if(Time.time-endingGame>=2f){require(pendingSamples>0,"No transition wait observed");note("Ending retained for two game seconds; no Main retry");finish("PASS");}
  }catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
 };
 UnityEditor.EditorApplication.update+=tick;
}catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
return "Scheduled simultaneous-death probe; inspect probe-progress.txt";
'''

pause = r'''
try{
 var pause=UnityEngine.Object.FindFirstObjectByType<PauseManager>();
 var transition=UnityEngine.Object.FindFirstObjectByType<SceneTransitionManager>();
 require(pause!=null&&transition!=null,"Pause/Transition manager missing");
 var leaving=typeof(PauseManager).GetField("leavingScene",flags);
 var confirm=typeof(PauseManager).GetMethod("ConfirmReturnToTitle",flags);
 require(leaving!=null&&confirm!=null,"Pause confirmation members missing");
 transition.fadeDuration=0.15f;
 typeof(SceneTransitionManager).GetField("minLoadingSeconds",flags).SetValue(transition,0.4f);
 typeof(SceneTransitionManager).GetField("maxLoadingSeconds",flags).SetValue(transition,0.4f);
 // Native Pause remembers a running scene's time scale, then freezes it.
 Time.timeScale=1f;pause.Pause();require(pause.IsPaused&&Time.timeScale==0f,"Pause could not enter");
 __REQUEST__
 confirm.Invoke(pause,null);
 require((bool)leaving.GetValue(pause)&&pause.IsPaused&&Time.timeScale==0f,"Confirmation did not freeze old Main");
 float hpAfter=status.HP,gateAfter=status.GATE;int stageAfter=StageCounter.Instance.StageCount;
 bool menu=pause.pauseMenuPanel!=null&&pause.pauseMenuPanel.activeSelf;
 bool inventory=pause.inventoryPanel!=null&&pause.inventoryPanel.activeSelf;
 int waitingSamples=0;double arrivedAt=-1;int targetHandle=-1;
 tick=()=>{
  try{
   if(!Application.isPlaying){finish("ABORTED");return;}
   require(UnityEditor.EditorApplication.timeSinceStartup-wallStart<30,"Probe wall timeout");
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   if(scene.handle==initialScene.handle){
    require(Time.timeScale==0f,"Old Main resumed while transition was pending");
    pause.Resume();pause.Back();pause.Pause();pause.RetryDay();pause.ToggleInventory();pause.CloseInventory();confirm.Invoke(pause,null);
    require((bool)leaving.GetValue(pause)&&pause.IsPaused&&Time.timeScale==0f,"Repeated Pause input changed leaving state");
    require((pause.pauseMenuPanel!=null&&pause.pauseMenuPanel.activeSelf)==menu&&(pause.inventoryPanel!=null&&pause.inventoryPanel.activeSelf)==inventory,"Repeated input changed menus during departure");
    require(status.HP==hpAfter&&Mathf.Approximately(status.GATE,gateAfter)&&StageCounter.Instance.StageCount==stageAfter,"Old gameplay changed restored state during departure");
    waitingSamples++;return;
   }
   require(scene.name=="__TARGET__","Unexpected target scene: "+scene.name);
   require(Time.timeScale==1f,"Loaded scene did not resume timeScale1");
   if(arrivedAt<0){arrivedAt=UnityEditor.EditorApplication.timeSinceStartup;targetHandle=scene.handle;note("target loaded "+scene.name+" handle="+scene.handle+" wait samples="+waitingSamples);}
   require(scene.handle==targetHandle,"Repeated input caused a second scene reload");
   if(UnityEditor.EditorApplication.timeSinceStartup-arrivedAt>=0.4){require(waitingSamples>0,"No pending interval observed");finish("PASS");}
  }catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
 };
 UnityEditor.EditorApplication.update+=tick;
}catch(System.Exception ex){note(ex.ToString());finish("FAIL");}
return "Scheduled Pause transition probe; inspect probe-progress.txt";
'''

bodies = {
    'ProbeGateDamage': ('gate-damage', gate),
    'ProbeBoostLifetime': ('boost-lifetime', boost),
    'ProbeDeathPlayerFirst': ('death-player-first', death.replace('__DAMAGE_ORDER__', 'player.TakeDamageByBullet(2f);gate.TakeDamage(2f);').replace('__ORDER_LABEL__','player_then_gate')),
    'ProbeDeathGateFirst': ('death-gate-first', death.replace('__DAMAGE_ORDER__', 'gate.TakeDamage(2f);player.TakeDamageByBullet(2f);').replace('__ORDER_LABEL__','gate_then_player')),
    'ProbePauseRetry': ('pause-retry', pause.replace('__REQUEST__','pause.RetryDay();').replace('__TARGET__','Main')),
    'ProbePauseTitle': ('pause-title', pause.replace('__REQUEST__','typeof(PauseManager).GetMethod("ShowTitleReturnConfirm",flags).Invoke(pause,null);').replace('__TARGET__','Title')),
}
for name, (label, body) in bodies.items():
    (folder / (name+'.cs.txt')).write_text(common.replace('__LABEL__', label) + body)

restore=(previous/'RestoreSession.cs.txt').read_text()
restore='if(System.IO.File.Exists("'+dest+'probe-progress.txt")&&System.IO.File.ReadAllText("'+dest+'probe-progress.txt").StartsWith("RUNNING"))throw new System.Exception("Stop/finish local regression callback before restoration");\n'+restore
restore=restore.replace('folder + "final-restoration.txt"','"'+dest+'final-restoration.txt"')
(folder/'RestoreReleaseSession.cs.txt').write_text(restore)
(folder/'probe-preparation-status.json').write_text(json.dumps({'prepared':list(bodies),'executed_by_preparer':False,'fresh_backup_folder':backup,'bootstrap_input':'GearCraft,4,none,77','notes':'Unity execution and restoration verification pending root. Static compile is reported separately.'},ensure_ascii=False,indent=2)+'\n')
print('Generated bootstrap, six independent probes, restoration adapter. No Unity execution.')
