#!/usr/bin/env python3
"""Apply native-input bot corrections only, preserving active case plans and backups."""
from pathlib import Path
import hashlib

HERE=Path(__file__).resolve().parent
MARKER="// QA_BOT_VISIBLE_PROJECTILE_JUMP_AND_INTERCEPT_LEAD_V1"


def once(text,before,after):
    if text.count(before)!=1:
        raise ValueError(f"Expected one anchor: {text.count(before)} {before[:100]}")
    return text.replace(before,after,1)


def patch_driver(route):
    if MARKER in route:
        return patch_boost_reach(route)
    route=once(route,'int reactivePreviousTarget = 0;', '''int reactivePreviousTarget = 0;
// QA_BOT_VISIBLE_PROJECTILE_JUMP_AND_INTERCEPT_LEAD_V1
bool reactiveJumpRequested=false;
float reactiveNextJump=Time.time;
var barrierActiveField=typeof(PlayerController).GetField("isBarrierActive",flags);
var barrierChargingField=typeof(PlayerController).GetField("isBarrierRecharging",flags);
bool? observedBarrierActive=null,observedBarrierCharging=null;
int observedDefenseHP=status.HP;
''')
    route=once(route,' if(best!=null && bestTime<(bestIsBomb?1f:0.45f)', '''
 // A real jump, available on the ground, can clear a low straight bullet that a
 // horizontal dash crosses. Only react to projectiles already visible to the bot.
 bool plannedJump=false;
 if(best!=null&&!bestIsBomb&&best.GetComponent<FinalBossMissile>()==null&&
    bestTime>0.08f&&bestTime<0.65f&&Time.time>=reactiveNextJump&&Mathf.Abs(body.linearVelocity.y)<0.35f){
  bool onFloor=Physics2D.BoxCastAll(ownCenter,(Vector2)ownBounds.size*0.9f,0f,Vector2.down,0.16f)
   .Any(hit=>hit.collider!=null&&hit.collider!=playerCollider&&!hit.collider.isTrigger&&hit.collider.CompareTag("Ground"));
  bool clearAbove=!Physics2D.BoxCastAll(ownCenter,(Vector2)ownBounds.size*0.92f,0f,Vector2.up,1.3f)
   .Any(hit=>hit.collider!=null&&hit.collider!=playerCollider&&!hit.collider.isTrigger&&
    (hit.collider.CompareTag("Wall")||hit.collider.GetComponent<EnemyController>()!=null));
  float horizon=Mathf.Clamp(bestTime,0.12f,0.65f);
  float jumpRise=player.jumpForce*horizon+0.5f*Physics2D.gravity.y*body.gravityScale*horizon*horizon;
  float bulletTop=best.bounds.max.y+bestVelocity.y*horizon;
  if(onFloor&&clearAbove&&ownBounds.min.y+jumpRise>bulletTop+0.2f){
   reactiveJumpRequested=true;reactiveNextJump=Time.time+0.8f;plannedJump=true;
   record("reactive_jump_requested","visible="+best.name+";contact="+number(bestTime)+";native jumpForce="+number(player.jumpForce)+";no invulnerability");
  }
 }
 if(!plannedJump&&best!=null && bestTime<(bestIsBomb?1f:0.45f)''')
    # Prevent the movement routine from overwriting the requested jump with its prior vy.
    route=once(route,' float vy=body.linearVelocity.y;bool grounded=', ''' float vy=body.linearVelocity.y;
 if(reactiveJumpRequested){vy=player.jumpForce;reactiveJumpRequested=false;record("reactive_jump_applied","normal grounded jump impulse");}
 bool grounded=''')
    route=once(route,' var projectileBody=weapon.bulletPrefab!=null?', '''
 // Lead an interceptable moving projectile using its current visible velocity.
 // Normal enemy aiming and weapon gravity remain unchanged.
 if(intercept!=null){
  var interceptBody=intercept.GetComponent<Rigidbody2D>();
  if(interceptBody!=null){
   Vector2 targetVelocity=interceptBody.linearVelocity;
   Vector2 relative=(Vector2)aim-((Vector2)player.FirePoint.position+new Vector2(weapon.muzzleOffset.x,weapon.muzzleOffset.y));
   float a=targetVelocity.sqrMagnitude-speed*speed,b=2f*Vector2.Dot(relative,targetVelocity),c=relative.sqrMagnitude;
   float leadTime=travel,discriminant=b*b-4f*a*c;
   if(Mathf.Abs(a)<0.001f){if(Mathf.Abs(b)>0.001f&&-c/b>0f)leadTime=-c/b;}
   else if(discriminant>=0f){
    float first=(-b-Mathf.Sqrt(discriminant))/(2f*a),second=(-b+Mathf.Sqrt(discriminant))/(2f*a);
    if(first>0f&&second>0f)leadTime=Mathf.Min(first,second);else if(first>0f)leadTime=first;else if(second>0f)leadTime=second;
   }
   travel=Mathf.Clamp(leadTime,0f,1.5f);aim+=(Vector3)(targetVelocity*travel);
  }
 }
 var projectileBody=weapon.bulletPrefab!=null?''')
    route=once(route,' if(player.punkdrive!=null)player.punkdrive.SetActive(status.punkDrive);', '''
 if(player.punkdrive!=null)player.punkdrive.SetActive(status.punkDrive);
 bool barrierNow=barrierActiveField!=null&&(bool)barrierActiveField.GetValue(player);
 bool rechargingNow=barrierChargingField!=null&&(bool)barrierChargingField.GetValue(player);
 if(observedBarrierActive!=barrierNow||observedBarrierCharging!=rechargingNow||observedDefenseHP!=status.HP){
  record("defense_state","barrier="+barrierNow+";recharging="+rechargingNow+";HP_delta="+(status.HP-observedDefenseHP)+";x="+number(player.transform.position.x)+";y="+number(player.transform.position.y));
  observedBarrierActive=barrierNow;observedBarrierCharging=rechargingNow;observedDefenseHP=status.HP;
 }
''')
    route=route.replace('reactive_gate_v1;decision=0.2s;', 'reactive_gate_v2;visible-straight-projectile-jump;intercept-velocity-lead;decision=0.2s;')
    return patch_boost_reach(route)


def patch_boost_reach(route):
    marker="// QA_MELEE_BOOST_REQUIRES_REACH"
    if marker in route:return route
    return once(route,' if(vulnerableBoss){var finalAI=', ''' // QA_MELEE_BOOST_REQUIRES_REACH
 if(vulnerableBoss&&mainWeapon.weaponType==WeaponType.Melee){
  var targetCollider=enemyTarget.GetComponent<Collider2D>();
  if(targetCollider==null||Vector2.Distance(player.transform.position,targetCollider.bounds.ClosestPoint(player.transform.position))>3.8f)vulnerableBoss=false;
 }
 if(vulnerableBoss){var finalAI=''')


def main():
    for f in HERE.glob("*progress.txt"):
        if f.read_text().lstrip().startswith(("RUNNING","LOADING")):
            raise SystemExit("Do not replace a running QA body: "+f.name)
    source=HERE/"NativeInputSurvivalRoute.cs.txt";old=source.read_text();new=patch_driver(old)
    if old==new:
        print("Driver corrections already applied; no files changed.")
        return
    old_sha=hashlib.sha256(old.encode()).hexdigest();new_sha=hashlib.sha256(new.encode()).hexdigest()
    rewritten=[]
    for mode in ("Direct","Survival"):
        file=HERE/("Batch"+mode+".cs.txt");batch=file.read_text()
        start=batch.index("System.Func<string> bRunRoute=()=>{")
        end=batch.index("double bStartedAt=",start)
        embedded=once(new,"UnityEditor.EditorApplication.isPlaying=false;","Time.timeScale=0f; // Batch keeps Play until all cases finish.")
        embedded=once(embedded,"System.Func<BonusCardDataSO,float> cardScore=data=>{","bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{")
        batch=batch[:start]+"System.Func<string> bRunRoute=()=>{\n"+embedded+"\n};\n"+batch[end:]
        batch=once(batch,old_sha,new_sha)
        rewritten.append((file,batch))
    source.write_text(new)
    for file,batch in rewritten:file.write_text(batch)
    print("Patched QA jump/lead/defense diagnostics. Case plans, captured saves and production unchanged.")


if __name__=="__main__":
    main()
