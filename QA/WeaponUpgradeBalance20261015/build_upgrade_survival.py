from pathlib import Path
folder = Path('QA/WeaponUpgradeBalance20261015')
src=Path('QA/WeaponBalance20261015/NativeSurvivalBootstrap.cs.txt').read_text().replace('QA/WeaponBalance20261015/','QA/WeaponUpgradeBalance20261015/')
src=src.replace('profile(common|mature)', 'profile(loadouts.csv ID)').replace('RailCraft,19,mature,77','RailCraft,19,late,77').replace('common|mature','profile')
src=src.replace('!line.TrimStart().StartsWith("#")).ToArray();','!line.TrimStart().StartsWith("#")&&!line.TrimStart().StartsWith("weapon,")).ToArray();')
src=src.replace('if(profile!="common"&&profile!="mature")throw new System.Exception("Profile must be common or mature");','''var loadoutLines=System.IO.File.ReadAllLines(folder+"loadouts.csv").Where(line=>!string.IsNullOrWhiteSpace(line)&&!line.TrimStart().StartsWith("#")&&!line.TrimStart().StartsWith("profile,")).Select(line=>line.Split(',')).Where(row=>row.Length==3&&row[0].Trim()==profile).ToArray();
if(loadoutLines.Length!=1)throw new System.Exception("Expected exactly one matching loadout: "+profile);
int moduleDamage=int.Parse(loadoutLines[0][2],System.Globalization.CultureInfo.InvariantCulture);
var partNames=loadoutLines[0][1].Split(';').Where(name=>!string.IsNullOrWhiteSpace(name)).Select(name=>name.Trim()).OrderBy(name=>name).ToArray();
var placementLines=System.IO.File.ReadAllLines(folder+"loadout-placements.csv").Where(line=>!string.IsNullOrWhiteSpace(line)&&!line.TrimStart().StartsWith("#")&&!line.TrimStart().StartsWith("profile,")).Select(line=>line.Split(',')).Where(row=>row.Length==5&&row[0].Trim()==profile).ToArray();
if(!partNames.SequenceEqual(placementLines.Select(row=>row[1].Trim()).OrderBy(name=>name).ToArray()))throw new System.Exception("Placement assets/counts do not exactly match loadout "+profile);
var placements=new System.Collections.Generic.List<StatusManager.SavedUpgradePartPlacement>();
foreach(var placementRow in placementLines){
 string assetName=placementRow[1].Trim();var part=UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradePartSO>("Assets/GearCraft/ScriptableObject/UpgradeParts/"+assetName+".asset");
 if(part==null)throw new System.Exception("Upgrade part asset missing: "+assetName);
 var placement=new StatusManager.SavedUpgradePartPlacement();placement.partData=part;placement.gridX=int.Parse(placementRow[2]);placement.gridY=int.Parse(placementRow[3]);placement.rotation=int.Parse(placementRow[4]);placement.inventoryIndex=placements.Count;placements.Add(placement);
}''')
src=src.replace('status.STR=status.ACC=2*(day-1);status.bonusDamage=3f;status.attackSpeedMult=profile=="mature" ? 0.6561f : 1f;status.craftWeaponDamagebuff=profile=="mature"?20:0;', '''status.STR=status.ACC=2*(day-1);status.craftWeaponDamagebuff=moduleDamage;
 status.ownedUpgradeParts=placements.Select(placement=>placement.partData).ToList();status.savedUpgradePartPlacements=placements;
 status.RecalculateUpgradeEffects();''')
src=src.replace('// ResetRunProgress already disables modules/Punk/weapon-specific bonuses/double/ricochet and clears part placements.', '// ResetRunProgress disables modules/Punk/weapon-specific cards. Validated saved placements above preserve the same part effects after native EquipWeapon calls.')
src=src.replace('\\nflat_damage=3\\nattack_speed="+status.attackSpeedMult.ToString("F4",invariant)', '\\nflat_damage="+status.bonusDamage.ToString("R",invariant)+"\\nattack_speed="+status.attackSpeedMult.ToString("R",invariant)')
src=src.replace('"\\nasset="+UnityEditor.AssetDatabase.GetAssetPath(target)', '"\\nparts="+string.Join(";",partNames)+"\\nplacements="+string.Join(";",placementLines.Select(row=>string.Join(":",row)).ToArray())+"\\ndouble_shot="+status.hasBulletDouble+"\\nspread_modifier="+status.spreadModifier.ToString("R",invariant)+"\\nricochet="+status.ricochetCount+"\\nbullet_size="+status.bulletSizeMult.ToString("R",invariant)+"\\ninitial_hit_damage="+status.GetWeaponDamage(target).ToString("R",invariant)+"\\nasset="+UnityEditor.AssetDatabase.GetAssetPath(target)')
(folder/'NativeSurvivalBootstrap.cs.txt').write_text(src)

src=Path('QA/WeaponBalance20261015/NativeSurvivalRoute.cs.txt').read_text().replace('QA/WeaponBalance20261015/','QA/WeaponUpgradeBalance20261015/')
src=src.replace('int lastFrame=-1,shots=0,rewards=0,', 'int lastFrame=-1,shots=0,attackEvents=0,rewards=0,')
src=src.replace('var configure=typeof(PlayerController)', '''var staticFlags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
var calculateSpread=typeof(PlayerController).GetMethod("CalculateSpreadAngle",staticFlags);
var getBulletSpeed=typeof(PlayerController).GetMethod("GetCurrentBulletSpeed",flags);
var getMuzzle=typeof(PlayerController).GetMethod("GetMuzzlePosition",flags);
var lineAttack=typeof(PlayerController).GetMethod("PerformLineHitboxAttack",flags);
var gatlingRecoil=typeof(PlayerController).GetMethod("ApplySteamGatlingDownwardRecoil",flags);
if(calculateSpread==null||getBulletSpeed==null||getMuzzle==null||lineAttack==null||gatlingRecoil==null)throw new System.Exception("Native shot methods missing");
var configure=typeof(PlayerController)''')
src=src.replace(';var bulletSpeedMethod=typeof(PlayerController).GetMethod("GetCurrentBulletSpeed",flags);', ';')
src=src.replace('gate_damage,details\\n', 'gate_damage,attack_events,attack_speed,double_shot,spread_modifier,ricochet,bullet_size,details\\n')
src=src.replace('gateDamage.ToString("F2",invariant),details};', 'gateDamage.ToString("F2",invariant),attackEvents.ToString(),status!=null?status.attackSpeedMult.ToString("R",invariant):"",status!=null?status.hasBulletDouble.ToString():"",status!=null?status.spreadModifier.ToString("R",invariant):"",status!=null?status.ricochetCount.ToString():"",status!=null?status.bulletSizeMult.ToString("R",invariant):"",details};')
src=src.replace('"\\nshots="+shots+"\\nplayer_damage="', '"\\nshots="+shots+"\\nattack_events="+attackEvents+"\\nplayer_damage="')
src=src.replace('Bot aim/movement and attacks derive from CombatBenchmark.cs.txt including', 'Initial parts come from loadouts.csv plus validated saved placements; RecalculateUpgradeEffects applies native effects and retains them across re-equips. Actual native spread/muzzle/speed/line/projectile configuration/recoil methods are called, including native double-shot branch. shots counts projectiles, attack_events counts attacks. Bot aim/movement and attacks derive from UpgradeCombatBenchmark.cs.txt including')
src=src.replace('System.IO.File.WriteAllText(folder+label+"-initial-status.json",JsonUtility.ToJson(status,true));', '''System.IO.File.WriteAllText(folder+label+"-initial-status.json",JsonUtility.ToJson(status,true));
if(System.IO.File.Exists(folder+"native-survival-bootstrap-config-used.txt"))System.IO.File.Copy(folder+"native-survival-bootstrap-config-used.txt",folder+label+"-bootstrap-config.txt",true);''')
baseline=(folder/'UpgradeCombatBenchmark.cs.txt').read_text()
start=baseline.index(' // Choose nearest threat to gate,')
end=baseline.index('\n }catch(System.Exception ex)',start)
block=baseline[start:end]
block=block.replace('status.currentWeapon=desired;player.currentWeapon=desired;', 'status.EquipWeapon(desired);player.currentWeapon=desired;')
# Native daily rewards may grant these two bonuses; preserve their real movement/ballistic impact.
block=block.replace('Mathf.Sign(dx)*player.moveSpeed;', 'Mathf.Sign(dx)*(player.moveSpeed+(weapon.weaponName=="GearCraft_Sword"?status.gearCraftSwordMoveSpeedBonus:0f));')
block=block.replace('float gravity=projectileBody!=null?projectileBody.gravityScale:0f;', 'float gravity=weapon.weaponName=="SteamThrower"&&status.steamThrowerNoBulletGravity?0f:projectileBody!=null?projectileBody.gravityScale:0f;')
start=src.index(' // BOT_SHARED_BLOCK_BEGIN:')
end=src.index(' // BOT_SHARED_BLOCK_END',start)+len(' // BOT_SHARED_BLOCK_END')
src=src[:start]+' // BOT_SHARED_BLOCK_BEGIN: native part-aware shots, benchmark aim policy.\n'+block+' // BOT_SHARED_BLOCK_END'+src[end:]
src=src.replace('var staticFlags=', '''var coolTimeField=typeof(PlayerController).GetField("coolTime",flags);
var coolTimeUpdate=typeof(PlayerController).GetMethod("CoolTimeUpdate",flags);
if(coolTimeField==null||coolTimeUpdate==null)throw new System.Exception("Native cooldown members missing");
float oldCoolTime=(float)coolTimeField.GetValue(player);
var staticFlags=''',1)
src=src.replace('float started=Time.time,nextShot=Time.time,nextJump=', 'float started=Time.time,nextJump=')
src=src.replace('if(player!=null)player.enabled=oldPlayerEnabled;', 'if(player!=null){coolTimeField.SetValue(player,oldCoolTime);player.enabled=oldPlayerEnabled;}')
src=src.replace('player.enabled=false;arm.enabled=false;', 'player.enabled=false;arm.enabled=false;coolTimeField.SetValue(player,0f);',1)
src=src.replace('tick=()=>{try{', 'tick=()=>{bool processCooldown=false;try{',1)
src=src.replace('lastFrame=Time.frameCount;lastFrameAt=now;frames++;', 'lastFrame=Time.frameCount;lastFrameAt=now;frames++;processCooldown=true;',1)
src=src.replace('finish("failed_"+ex.Message);}};', '''finish("failed_"+ex.Message);}
 finally{
  // Also advances while native UI blocks attacks; no decrement after restoration.
  if(processCooldown&&!done&&Application.isPlaying&&player!=null&&!player.enabled&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main")coolTimeUpdate.Invoke(player,null);
 }
};''')
src=src.replace('shots counts projectiles,', 'Cooldown uses the native private timer and one CoolTimeUpdate call after each game frame including blocked/no-target frames. shots counts projectiles,')
assert 'nextShot' not in src
(folder/'NativeSurvivalRoute.cs.txt').write_text(src)
