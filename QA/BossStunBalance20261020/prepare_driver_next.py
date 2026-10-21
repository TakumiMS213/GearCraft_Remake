#!/usr/bin/env python3
"""Small QA-only native dodge correction; stage .next by default, --install when idle."""
from pathlib import Path
import hashlib
import sys

HERE=Path(__file__).resolve().parent
MARKER="// QA_VISIBLE_SWEEP_AND_EVADE_HOLD_V1"

def once(text,before,after):
    if text.count(before)!=1:raise ValueError("Anchor count "+str(text.count(before))+": "+before[:90])
    return text.replace(before,after,1)

def patch(route):
    if MARKER in route:return route
    route=once(route,"float reactiveNextJump=Time.time;",'''float reactiveNextJump=Time.time;
// QA_VISIBLE_SWEEP_AND_EVADE_HOLD_V1
float reactiveEvadeHoldUntil=Time.time;
// Segment against an expanded rectangle: covers travel between samples, not endpoint only.
System.Func<Vector2,Vector2,Vector2,bool> reactiveSweepHits=(relative,delta,extents)=>{
 float enter=0f,exit=1f;
 for(int axis=0;axis<2;axis++){
  float p=axis==0?relative.x:relative.y,v=axis==0?delta.x:delta.y,e=axis==0?extents.x:extents.y;
  if(Mathf.Abs(v)<0.0001f){if(Mathf.Abs(p)>e)return false;}
  else{float a=(-e-p)/v,b=(e-p)/v;enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));if(enter>exit)return false;}
 }
 return exit>=0f&&enter<=1f;
};''')
    anchor=" // A real jump, available on the ground, can clear a low straight bullet that a"
    route=once(route,anchor,''' // Predict only currently visible projectiles from their current velocity.
 // A jump holds horizontal position; a dash holds its endpoint briefly.
 // Existing physics, jumpForce, dash speed and duration remain the sole movement limits.
 System.Func<bool,int,bool> safeVisiblePath=(jump,dashDirection)=>{
  float horizon=jump?1.05f:0.65f,dt=0.05f;
  float gravity=Physics2D.gravity.y*body.gravityScale;
  foreach(var projectile in threatColliders){
   if(projectile==null||!projectile.enabled||!projectile.gameObject.activeInHierarchy)continue;
   var projectileBody=projectile.GetComponent<Rigidbody2D>();if(projectileBody==null)continue;
   Vector3 viewport=camera!=null?camera.WorldToViewportPoint(projectile.bounds.center):new Vector3(-1f,-1f,-1f);
   if(viewport.z<=0f||viewport.x<0f||viewport.x>1f||viewport.y<0f||viewport.y>1f)continue;
   Vector2 projectileStart=projectile.bounds.center,projectileVelocity=projectileBody.linearVelocity;
   if((projectileStart-ownCenter).sqrMagnitude>144f)continue;
   Vector2 extents=(Vector2)ownBounds.extents+(Vector2)projectile.bounds.extents+Vector2.one*0.08f;
   Vector2 priorRelative=projectileStart-ownCenter;
   for(float t=dt;t<=horizon+0.001f;t+=dt){
    float rise=jump?Mathf.Max(0f,player.jumpForce*t+0.5f*gravity*t*t):body.linearVelocity.y*t+0.5f*gravity*t*t;
    if(!jump&&Mathf.Abs(body.linearVelocity.y)<0.35f)rise=0f;
    Vector2 playerAt=ownCenter+new Vector2(dashDirection*player.dashSpeed*Mathf.Min(t,player.dashDuration),rise);
    Vector2 projectileAt=projectileStart+projectileVelocity*t+Vector2.up*(0.5f*Physics2D.gravity.y*projectileBody.gravityScale*t*t);
    Vector2 relative=projectileAt-playerAt;
    if(reactiveSweepHits(priorRelative,relative-priorRelative,extents))return false;
    priorRelative=relative;
   }
  }
  return true;
 };
''' + anchor)
    route=once(route,"if(onFloor&&clearAbove&&ownBounds.min.y+jumpRise>bulletTop+0.2f){","if(onFloor&&clearAbove&&ownBounds.min.y+jumpRise>bulletTop+0.2f&&safeVisiblePath(true,0)){")
    route=once(route,"reactiveJumpRequested=true;reactiveNextJump=Time.time+0.8f;plannedJump=true;","reactiveJumpRequested=true;reactiveNextJump=Time.time+1.05f;reactiveEvadeHoldUntil=Time.time+1.05f;plannedJump=true;")
    route=once(route,"if(!plannedJump&&best!=null && bestTime<","if(!plannedJump&&Time.time>=reactiveEvadeHoldUntil&&best!=null && bestTime<")
    route=once(route,"   if(bodyBlocked)continue;","   if(bodyBlocked||!safeVisiblePath(false,direction))continue;")
    route=once(route,'if(chosen!=0){reactiveStartDash.Invoke(player,new object[]{chosen});record("reactive_dash",chosen+";native duration/speed;no invincibility");}',
        'if(chosen!=0){reactiveEvadeHoldUntil=Time.time+0.65f;reactiveStartDash.Invoke(player,new object[]{chosen});record("reactive_dash",chosen+";visible swept path checked;hold endpoint 0.65s;native duration/speed;no invincibility");}')
    route=once(route," if(timer<=0f)return ordinaryVx;"," if(timer<=0f)return Time.time<reactiveEvadeHoldUntil?0f:ordinaryVx;")
    route=once(route,"bounds.center.y-player.transform.position.y>2.7f","bounds.min.y-playerCollider.bounds.max.y>0.35f")
    route=route.replace("reactive_gate_v2;visible-straight-projectile-jump;","reactive_gate_v3;all-visible-swept-jump-dash;brief-endpoint-hold;melee-lower-edge;")
    return route

def main():
    source=HERE/"NativeInputSurvivalRoute.cs.txt"
    old=source.read_text();new=patch(old)
    next_file=HERE/"NativeInputSurvivalRoute.next.cs.txt"
    next_file.write_text(new)
    print("Prepared",next_file)
    if "--install" not in sys.argv:return
    for path in HERE.glob("*progress.txt"):
        if path.read_text().lstrip().startswith(("RUNNING","LOADING")):
            raise SystemExit("Refuse installing during active batch: "+path.name)
    if old==new:
        print("Already installed");return
    old_sha=hashlib.sha256(old.encode()).hexdigest();new_sha=hashlib.sha256(new.encode()).hexdigest()
    batches=[]
    for mode in ("Direct","Survival"):
        path=HERE/("Batch"+mode+".cs.txt");batch=path.read_text()
        start=batch.index("System.Func<string> bRunRoute=()=>{")
        end=batch.index("double bStartedAt=",start)
        embedded=once(new,"UnityEditor.EditorApplication.isPlaying=false;","Time.timeScale=0f; // Batch keeps Play until all cases finish.")
        embedded=once(embedded,"System.Func<BonusCardDataSO,float> cardScore=data=>{","bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{")
        batch=batch[:start]+"System.Func<string> bRunRoute=()=>{\n"+embedded+"\n};\n"+batch[end:]
        batch=once(batch,old_sha,new_sha)
        batches.append((path,batch))
    source.write_text(new)
    for path,batch in batches:path.write_text(batch)
    print("Installed route and embedded batches; case plans and their hashes unchanged.")

if __name__=="__main__":main()
