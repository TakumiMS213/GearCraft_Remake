from pathlib import Path
src = Path('QA/WeaponBalance20261015/CombatBenchmark.cs.txt').read_text()
src = src.replace('const string folder="QA/WeaponBalance20261015/";', 'const string folder="QA/WeaponUpgradeBalance20261015/";')
src = src.replace('var configure=typeof(PlayerController)', '''var staticFlags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
var calculateSpread=typeof(PlayerController).GetMethod("CalculateSpreadAngle",staticFlags);
var getBulletSpeed=typeof(PlayerController).GetMethod("GetCurrentBulletSpeed",flags);
var getMuzzle=typeof(PlayerController).GetMethod("GetMuzzlePosition",flags);
var lineAttack=typeof(PlayerController).GetMethod("PerformLineHitboxAttack",flags);
var gatlingRecoil=typeof(PlayerController).GetMethod("ApplySteamGatlingDownwardRecoil",flags);
if(calculateSpread==null||getBulletSpeed==null||getMuzzle==null||lineAttack==null||gatlingRecoil==null)throw new System.Exception("Native shot methods missing");
if(player.runtimeStatus!=status)throw new System.Exception("Player Start has not bound runtime StatusManager");
var configure=typeof(PlayerController)''')
src = src.replace('!x.StartsWith("#")).ToArray();', '!x.StartsWith("#")&&!x.StartsWith("id,")).ToArray();', 1)
anchor = 'string label=System.IO.File.ReadAllText(folder+"batch-name.txt").Trim();'
insertion = '''var profileParts=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<UpgradePartSO>>();
var profileModules=new System.Collections.Generic.Dictionary<string,int>();
foreach(var line in System.IO.File.ReadAllLines(folder+"loadouts.csv")){
 if(string.IsNullOrWhiteSpace(line)||line.StartsWith("#")||line.StartsWith("profile,"))continue;
 var values=line.Split(',');if(values.Length!=3)throw new System.Exception("Invalid loadout row: "+line);
 var parts=new System.Collections.Generic.List<UpgradePartSO>();
 foreach(var assetName in values[1].Split(';')){
  if(string.IsNullOrWhiteSpace(assetName))continue;
  var part=UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradePartSO>("Assets/GearCraft/ScriptableObject/UpgradeParts/"+assetName.Trim()+".asset");
  if(part==null)throw new System.Exception("Upgrade part not found: "+assetName);
  parts.Add(part);
 }
 profileParts.Add(values[0],parts);profileModules.Add(values[0],int.Parse(values[2],System.Globalization.CultureInfo.InvariantCulture));
}
foreach(var line in sourceLines){var values=line.Split(',');if(values.Length<6||!profileParts.ContainsKey(values[3]))throw new System.Exception("Case references invalid profile: "+line);}
'''
src=src.replace(anchor,insertion+anchor)
src=src.replace('scheduled_remaining\\n");','scheduled_remaining,attack_events\\n");',1)
start=src.index('System.IO.File.WriteAllText(folder+label+"-meta.txt",')
end=src.index('\nvar scheduledEnemies=',start)
src=src[:start]+'''System.IO.File.WriteAllText(folder+label+"-meta.txt","Native EnemySpawner planning methods generate a seeded fixed spawn replay using native prefabs/AI/physics and 1/60 game delta. Physics step unchanged. Actual spawn time/position recorded separately. Bot controls movement/aim; aim compensates muzzleOffset. HP/GATE replenished each frame, so these are offensive throughput tests, not human ordinary clear times or survival proofs. Each wave resets. STR=ACC=2*(day-1), no unique reward cards, no PunkDrive, no BOOST. Upgrade effects are read from actual UpgradePartSO assets and applied through StatusManager.RecalculateUpgradeEffects(parts), with no direct fixed bonusDamage assignment. moduleDamage is separate crafted module damage. Native spread/muzzle/speed/configuration/line-hit/recoil methods are invoked; double-shot duplicates the native branch. shots counts projectiles (or melee/line attacks), attack_events counts attacks. Original PlayerPrefs are not restored by this script; root restoration is required after all tests. No crafting/ED time counted.\\n");
System.IO.File.WriteAllText(folder+label+"-loadout-effects.csv","case,weapon,profile,part_index,asset,part_name,ranged_only,applied,effect_type,value\\n");
System.IO.File.WriteAllText(folder+label+"-loadout-stats.csv","case,weapon,profile,STR,ACC,base_damage,damage_per_hit,flat_damage,module_damage,cooldown_multiplier,double_shot,spread_modifier,ricochet,bullet_size,junk_collector,magnet_range,base_spread,effective_spread,bullet_speed\\n");
System.Func<float,string> fmt=x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
''' + src[end:]
src=src.replace('shots=0,frames=0;', 'shots=0,frames=0,attackEvents=0;')
src=src.replace('scheduledEnemies.Count(e=>e!=null&&e.HP>0f&&e.GetComponent<Collider2D>().enabled).ToString()});','scheduledEnemies.Count(e=>e!=null&&e.HP>0f&&e.GetComponent<Collider2D>().enabled).ToString(),attackEvents.ToString()});')
start=src.index('  status.bossKillCount=')
end=src.index('\n  mainWeapon=',start)
src=src[:start]+'''  status.bossKillCount=stage>30?3:stage>18?2:stage>9?1:0;status.STR=status.ACC=2*(day-1);status.craftWeaponDamagebuff=profileModules[row[3]];
  status.module_barrier=false;status.module_repair=false;status.punkDrive=false;status.HP=100;status.GATE=10000;
  status.gearCraftAxeSizeMultiplier=1f;status.gearCraftSwordMoveSpeedBonus=0f;status.gearCraftTransformGearGain=0;
  status.steamCannonBulletSpeedMultiplier=1f;status.steamCannonExplosionRadiusMultiplier=1f;status.steamCannonGiantBulletChance=0f;status.steamCannonDirectHitKnockback=0f;
  status.steamThrowerOverheatSlipDamage=0f;status.steamThrowerNoBulletGravity=false;status.steamThrowerBoostDamageMultiplier=1f;status.steamThrowerBoostBurnDrops=false;status.steamThrowerBoostBurnDropsActive=false;
  status.railCraftApplyOverheat=false;status.railCraftBulletSizeMultiplier=1f;status.railCraftRicochetMultiplier=1f;
  status.steamGatlingBulletSpeedMultiplier=1f;status.steamGatlingBossDamageMultiplier=1f;status.steamGatlingNormalDamageMultiplier=1f;status.steamGatlingDownwardRecoil=false;
  typeof(PlayerController).GetField("isBoostActive",flags).SetValue(player,false);
''' + src[end:]
src=src.replace('  status.currentWeapon=mainWeapon;player.currentWeapon=mainWeapon;player.STR=status.STR;', '''  if(mainWeapon==null||sword==null||axe==null)throw new System.Exception("Weapon asset missing for "+weaponName);
  status.currentWeapon=mainWeapon;status.RecalculateUpgradeEffects(profileParts[row[3]]);
  int partIndex=0;foreach(var part in profileParts[row[3]]){
   bool applied=!part.rangedOnly||mainWeapon.weaponType==WeaponType.Ranged;
   if(part.effects!=null)foreach(var effect in part.effects){if(effect==null)continue;
    System.IO.File.AppendAllText(folder+label+"-loadout-effects.csv",string.Join(",",new[]{row[0],weaponName,row[3],partIndex.ToString(),part.name,part.partName.Replace(",",";"),part.rangedOnly.ToString(),applied.ToString(),effect.type.ToString(),fmt(effect.value)})+"\\n");
   }partIndex++;
  }
  player.currentWeapon=mainWeapon;player.STR=status.STR;''')
src=src.replace('sync.Invoke(player,null);updateAnim.Invoke(player,null);\n  player.transform.position=', '''sync.Invoke(player,null);updateAnim.Invoke(player,null);
  System.IO.File.AppendAllText(folder+label+"-loadout-stats.csv",string.Join(",",new[]{row[0],weaponName,row[3],status.STR.ToString(),status.ACC.ToString(),fmt(mainWeapon.baseDamage),fmt(status.GetWeaponDamage(mainWeapon)),fmt(status.bonusDamage),status.craftWeaponDamagebuff.ToString(),fmt(status.attackSpeedMult),status.hasBulletDouble.ToString(),fmt(status.spreadModifier),status.ricochetCount.ToString(),fmt(status.bulletSizeMult),fmt(status.junkCollectorMult),fmt(status.magnetRange),fmt(mainWeapon.spreadAngle),fmt(Mathf.Max(0f,mainWeapon.spreadAngle+status.spreadModifier)),fmt((float)getBulletSpeed.Invoke(player,null))})+"\\n");
  player.transform.position=''')
src=src.replace('shots=frames=0;', 'shots=frames=attackEvents=0;')
src=src.replace('meleeAttack.Invoke(player,null);shots++;nextShot=', 'damageField.SetValue(player,weapon.baseDamage);meleeAttack.Invoke(player,null);shots++;attackEvents++;nextShot=')
src=src.replace('float speed=weapon.bulletSpeed;', 'float speed=(float)getBulletSpeed.Invoke(player,null);')
start=src.index(' var savedRandom=UnityEngine.Random.state;UnityEngine.Random.state=aimRandom;float finalAngle=')
end=src.index('\n }catch(System.Exception ex)',start)
src=src[:start]+''' // Native PerformShotAttack parity, except the bot supplies base aim instead of Input.mousePosition.
 // Include ConfigureSpawnedProjectile in the isolated shot RNG stream so both native branches consume randomness in their original order.
 var savedRandom=UnityEngine.Random.state;UnityEngine.Random.state=aimRandom;
 try{
  float spread=weapon.spreadAngle+status.spreadModifier;
  float finalAngle=(float)calculateSpread.Invoke(null,new object[]{angle,spread});
  dir=new Vector2(Mathf.Cos(finalAngle*Mathf.Deg2Rad),Mathf.Sin(finalAngle*Mathf.Deg2Rad));
  Vector3 muzzle=(Vector3)getMuzzle.Invoke(player,new object[]{dir});
  damageField.SetValue(player,weapon.baseDamage);
  if(weapon.useLineHitbox){lineAttack.Invoke(player,new object[]{dir,finalAngle,muzzle});shots++;}
  else{
   var bullet=UnityEngine.Object.Instantiate(weapon.bulletPrefab,muzzle,Quaternion.Euler(0f,0f,finalAngle));
   if(bullet==null)throw new System.Exception("Primary projectile Instantiate returned null");
   var rb=bullet.GetComponent<Rigidbody2D>();if(rb!=null)rb.linearVelocity=dir*(float)getBulletSpeed.Invoke(player,null);
   configure.Invoke(player,new object[]{bullet});shots++;
   if(status.hasBulletDouble){
    float secondAngle=(float)calculateSpread.Invoke(null,new object[]{angle,spread});
    Vector2 secondDirection=new Vector2(Mathf.Cos(secondAngle*Mathf.Deg2Rad),Mathf.Sin(secondAngle*Mathf.Deg2Rad));
    Vector3 secondMuzzle=(Vector3)getMuzzle.Invoke(player,new object[]{secondDirection});
    var secondBullet=UnityEngine.Object.Instantiate(weapon.bulletPrefab,secondMuzzle,Quaternion.Euler(0f,0f,secondAngle));
    if(secondBullet==null)throw new System.Exception("Secondary projectile Instantiate returned null");
    var secondBody=secondBullet.GetComponent<Rigidbody2D>();if(secondBody!=null)secondBody.linearVelocity=secondDirection*(float)getBulletSpeed.Invoke(player,null);
    configure.Invoke(player,new object[]{secondBullet});shots++;
   }
   gatlingRecoil.Invoke(player,new object[]{dir});
  }
  attackEvents++;nextShot=Time.time+weapon.coolTime*status.attackSpeedMult;
 }finally{aimRandom=UnityEngine.Random.state;UnityEngine.Random.state=savedRandom;}
''' + src[end:]
# Match PlayerController.Update's HandleAttack -> CoolTimeUpdate order, including
# blocked/no-target frames. Each Editor callback may run more than once per game
# frame, so only the callback that passes lastFrame may decrement the timer.
src=src.replace('var staticFlags=', '''var coolTimeField=typeof(PlayerController).GetField("coolTime",flags);
var coolTimeUpdate=typeof(PlayerController).GetMethod("CoolTimeUpdate",flags);
if(coolTimeField==null||coolTimeUpdate==null)throw new System.Exception("Native cooldown members missing");
float oldCoolTime=(float)coolTimeField.GetValue(player);
var staticFlags=''',1)
src=src.replace(',nextShot=0f,nextJump=0f;', ',nextJump=0f;')
src=src.replace('JsonUtility.FromJsonOverwrite(oldStatus,status);player.enabled=oldEnabled;', 'JsonUtility.FromJsonOverwrite(oldStatus,status);coolTimeField.SetValue(player,oldCoolTime);player.enabled=oldEnabled;')
src=src.replace('tick=()=>{try{', 'tick=()=>{bool processCooldown=false;try{',1)
src=src.replace('lastFrame=Time.frameCount;lastFrameAt=now;', 'lastFrame=Time.frameCount;lastFrameAt=now;processCooldown=true;',1)
src=src.replace('started=Time.time;nextShot=started;nextJump=started;', 'started=Time.time;coolTimeField.SetValue(player,0f);nextJump=started;',1)
src=src.replace('if(Time.time<nextShot)return;', 'if(!((float)coolTimeField.GetValue(player)<=0f))return;')
src=src.replace('damageField.SetValue(player,weapon.baseDamage);meleeAttack.Invoke(player,null);', 'coolTimeField.SetValue(player,weapon.coolTime*status.attackSpeedMult);damageField.SetValue(player,weapon.baseDamage);meleeAttack.Invoke(player,null);')
src=src.replace('nextShot=Time.time+weapon.coolTime*status.attackSpeedMult;', '')
src=src.replace('var savedRandom=UnityEngine.Random.state;UnityEngine.Random.state=aimRandom;\n try{', 'coolTimeField.SetValue(player,weapon.coolTime*status.attackSpeedMult);\n var savedRandom=UnityEngine.Random.state;UnityEngine.Random.state=aimRandom;\n try{')
src=src.replace('finish("FAILED "+ex.Message);}};', '''finish("FAILED "+ex.Message);}
 finally{
  // Native PlayerController resumes its own decrement after finish; never run both.
  if(processCooldown&&!done&&Application.isPlaying&&player!=null&&!player.enabled&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main")coolTimeUpdate.Invoke(player,null);
 }
};''')
src=src.replace('with gravity correction and small velocity lead.', 'with gravity correction.')
src=src.replace('shots counts projectiles (or melee/line attacks),', 'Cooldown uses the native private timer and one CoolTimeUpdate call after each game frame including no-target frames; shots counts projectiles (or melee/line attacks),')
assert 'nextShot' not in src
Path('QA/WeaponUpgradeBalance20261015/UpgradeCombatBenchmark.cs.txt').write_text(src)
