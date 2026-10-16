from pathlib import Path
folder=Path('QA/WeaponUpgradeBalance20261015')
s=(folder/'NativeSurvivalRoute.cs.txt').read_text()
s=s.replace('// C#6 execute_code method body.', '// Read-only bot instrumentation copy: movement/attacks/targeting unchanged. Use a unique native-survival-name.txt label.\n// C#6 execute_code method body.',1)
s=s.replace('string eventsPath=folder+label+"-events.csv";', '''string eventsPath=folder+label+"-events.csv";
if(System.IO.File.Exists(eventsPath))throw new System.Exception("Diagnostic requires a new label so existing native results cannot be overwritten: "+label);''',1)
anchor='UnityEditor.EditorApplication.CallbackFunction tick=null;'
insertion='''// Independent diagnostics only. Never sets a position, velocity, cooldown or damage value.
string diagnosticPath=folder+label+"-bot-diagnostic.csv";
EnemyController diagnosticTarget=null;int diagnosticTargetFrame=-1;string diagnosticReason="initial";
System.IO.File.WriteAllText(diagnosticPath,"game_seconds,frame,event,bot_reason,scene,player_x,player_y,player_z,velocity_x,velocity_y,player_bounds_min_x,player_bounds_min_y,player_bounds_max_x,player_bounds_max_y,player_collider_enabled,player_simulated,player_constraints,target_name,target_hp,target_x,target_y,target_z,target_min_x,target_min_y,target_max_x,target_max_y,target_collider_enabled,target_selection_frame,melee_distance,desired_x,dx,bot_grounded,input_blocked,flow_active,ending_loading,cooldown,attack_events,hp,gate,weapon,enemy_ai_state,ground_contact\\n");
System.Func<float,string> diagnosticFloat=value=>value.ToString("R",invariant);
System.Action<string> recordDiagnostic=eventName=>{
 var hasPlayer=player!=null;var hasBody=body!=null;var hasPlayerCollider=playerCollider!=null;
 Vector3 pp=hasPlayer?player.transform.position:Vector3.zero;Vector2 pv=hasBody?body.linearVelocity:Vector2.zero;
 Bounds pb=hasPlayerCollider?playerCollider.bounds:new Bounds();
 var targetCollider=diagnosticTarget!=null?diagnosticTarget.GetComponent<Collider2D>():null;
 bool hasTarget=diagnosticTarget!=null&&targetCollider!=null;
 Vector3 tp=hasTarget?diagnosticTarget.transform.position:Vector3.zero;Bounds tb=hasTarget?targetCollider.bounds:new Bounds();
 float distance=hasPlayer&&hasTarget?Vector2.Distance(pp,tb.ClosestPoint(pp)):-1f;
 float desired=hasTarget?Mathf.Clamp(tb.min.x-1.1f,-6.15f,6.4f):0f;
 bool grounded=hasPlayerCollider&&hasBody&&pb.min.y<=-4.38f&&Mathf.Abs(pv.y)<.35f;
 string aiState="";if(diagnosticTarget!=null){var ai=diagnosticTarget.GetComponent<EnemyAI_BossFinal>();if(ai!=null){var stateField=typeof(EnemyAI_BossFinal).GetField("state",flags);if(stateField!=null)aiState=stateField.GetValue(ai).ToString();}}
 bool groundContact=false;if(hasPlayerCollider){var contacts=new Collider2D[16];int count=playerCollider.GetContacts(contacts);for(int contactIndex=0;contactIndex<count;contactIndex++)if(contacts[contactIndex]!=null&&contacts[contactIndex].CompareTag("Ground")){groundContact=true;break;}}
 var values=new[]{diagnosticFloat(Time.time-started),Time.frameCount.ToString(),eventName,diagnosticReason,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
 hasPlayer?diagnosticFloat(pp.x):"",hasPlayer?diagnosticFloat(pp.y):"",hasPlayer?diagnosticFloat(pp.z):"",hasBody?diagnosticFloat(pv.x):"",hasBody?diagnosticFloat(pv.y):"",
 hasPlayerCollider?diagnosticFloat(pb.min.x):"",hasPlayerCollider?diagnosticFloat(pb.min.y):"",hasPlayerCollider?diagnosticFloat(pb.max.x):"",hasPlayerCollider?diagnosticFloat(pb.max.y):"",hasPlayerCollider?playerCollider.enabled.ToString():"",hasBody?body.simulated.ToString():"",hasBody?body.constraints.ToString():"",
 hasTarget?(diagnosticTarget.enemyData!=null?diagnosticTarget.enemyData.enemyName:diagnosticTarget.name):"",hasTarget?diagnosticFloat(diagnosticTarget.HP):"",hasTarget?diagnosticFloat(tp.x):"",hasTarget?diagnosticFloat(tp.y):"",hasTarget?diagnosticFloat(tp.z):"",hasTarget?diagnosticFloat(tb.min.x):"",hasTarget?diagnosticFloat(tb.min.y):"",hasTarget?diagnosticFloat(tb.max.x):"",hasTarget?diagnosticFloat(tb.max.y):"",hasTarget?targetCollider.enabled.ToString():"",diagnosticTargetFrame.ToString(),diagnosticFloat(distance),hasTarget?diagnosticFloat(desired):"",hasTarget&&hasPlayer?diagnosticFloat(desired-pp.x):"",grounded.ToString(),StageFlowManager.IsPlayerInputBlocked.ToString(),flow!=null?flow.IsStageActive.ToString():"",EndingManager.IsLoadingEnding.ToString(),hasPlayer?diagnosticFloat((float)coolTimeField.GetValue(player)):"",attackEvents.ToString(),status!=null?status.HP.ToString():"",status!=null?diagnosticFloat(status.GATE):"",status!=null&&status.currentWeapon!=null?status.currentWeapon.weaponName:"",aiState,groundContact.ToString()};
 System.IO.File.AppendAllText(diagnosticPath,string.Join(",",values.Select(csv).ToArray())+"\\n");
};
'''
assert anchor in s
s=s.replace(anchor,insertion+anchor,1)
s=s.replace('  record("finished",reason);','  recordDiagnostic("finished_"+reason);\n  record("finished",reason);',1)
s=s.replace('tick=()=>{bool processCooldown=false;try{','tick=()=>{bool processCooldown=false;diagnosticReason="prechecks";try{',1)
s=s.replace('openingConfirmed=true;record("opening_notice_confirmed"','diagnosticReason="opening_notice";openingConfirmed=true;record("opening_notice_confirmed"',1)
s=s.replace('if(report!=null&&report.IsShowing){body.linearVelocity=', 'if(report!=null&&report.IsShowing){diagnosticReason="day_report";body.linearVelocity=',1)
s=s.replace('if(rewardsManager!=null&&rewardsManager.IsChoosingReward){','if(rewardsManager!=null&&rewardsManager.IsChoosingReward){\n  diagnosticReason="reward_selection";',1)
s=s.replace('if(StageFlowManager.IsPlayerInputBlocked||!flow.IsStageActive||EndingManager.IsLoadingEnding){body.linearVelocity=', 'if(StageFlowManager.IsPlayerInputBlocked||!flow.IsStageActive||EndingManager.IsLoadingEnding){diagnosticReason=StageFlowManager.IsPlayerInputBlocked?"input_blocked":!flow.IsStageActive?"flow_inactive":"ending_loading";body.linearVelocity=',1)
s=s.replace(' bool melee=mainWeapon.weaponType==WeaponType.Melee;',' diagnosticTarget=target;diagnosticTargetFrame=Time.frameCount;\n bool melee=mainWeapon.weaponType==WeaponType.Melee;',1)
s=s.replace('if(target==null){body.linearVelocity=', 'if(target==null){diagnosticReason="no_visible_target";body.linearVelocity=',1)
s=s.replace('if(!((float)coolTimeField.GetValue(player)<=0f))return;', 'if(!((float)coolTimeField.GetValue(player)<=0f)){diagnosticReason="cooldown";return;}',1)
s=s.replace('if(Vector2.Distance(player.transform.position,bounds.ClosestPoint(player.transform.position))>3.8f)return;', 'if(Vector2.Distance(player.transform.position,bounds.ClosestPoint(player.transform.position))>3.8f){diagnosticReason="melee_out_of_range";return;}diagnosticReason="melee_attack";',1)
s=s.replace(' // Native PerformShotAttack parity,', ' diagnosticReason="ranged_attack";\n // Native PerformShotAttack parity,',1)
s=s.replace(' finally{\n  // Also advances while native UI blocks attacks;', ' finally{\n  if(processCooldown&&!done)recordDiagnostic("frame");\n  // Also advances while native UI blocks attacks;',1)
(folder/'NativeSurvivalGearDiagnostic.cs.txt').write_text(s)
