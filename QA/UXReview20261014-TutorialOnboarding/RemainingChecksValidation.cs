using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class RemainingChecksValidation { public static object Execute() {
// C#6 method body. Layout fixtures plus registered physics-trigger interactions.
// Capture and following mutations occur on different ticks. Cleanup closes guidance,
// restores eight backed-up preferences and GameView choices, then exits Play.
const string prefix="UXTutorialOnboarding20261014.";
const string folder="QA/UXReview20261014-TutorialOnboarding/";
if(!UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPaused || !UnityEditor.SessionState.GetBool(prefix+"backupReady",false))throw new System.Exception("Backed-up unpaused Play required");
if(UnityEditor.SessionState.GetBool(prefix+"remainingRunning",false))throw new System.Exception("Already running");
var prefs=System.IO.File.ReadAllLines(folder+"playerprefs-before-runtime.txt");if(prefs.Length!=8)throw new System.Exception("Incomplete backup");
var status=StatusManager.Instance;var materials=MaterialManager.Instance;
var c=UnityEngine.Object.FindFirstObjectByType<GearCraft.Scripts.Craft.CraftTutorialController>();
var tabs=UnityEngine.Object.FindFirstObjectByType<CraftTabController>();var craft=UnityEngine.Object.FindFirstObjectByType<CraftManager>();
if(status==null||materials==null||c==null||tabs==null||craft==null||!craft.IsReadyForTutorial)throw new System.Exception("Initialized Craft required");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
var asm=typeof(UnityEditor.Editor).Assembly;var vt=asm.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(vt)[0];
var st=asm.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(st).GetProperty("instance",flags).GetValue(null,null);
var group=st.GetProperty("currentGroup",flags).GetValue(sizes,null);var gt=group.GetType();
var originalLabels=(string[])gt.GetMethod("GetDisplayTexts",flags).Invoke(group,null);int originalIndex=UnityEditor.SessionState.GetInt(prefix+"gameSizeBefore",3);
var sizeType=asm.GetType("UnityEditor.GameViewSize");var modeType=asm.GetType("UnityEditor.GameViewSizeType");var mode=System.Enum.Parse(modeType,"FixedResolution");
int[] widths={1920,1600,1440},heights={1080,1000,1080};int[] indices=new int[3];
for(int i=0;i<3;i++){string label="UXTutorialOnboarding"+i;var labels=(string[])gt.GetMethod("GetDisplayTexts",flags).Invoke(group,null);int index=System.Array.FindIndex(labels,s=>s.StartsWith(label));if(index<0){var size=System.Activator.CreateInstance(sizeType,new object[]{mode,widths[i],heights[i],label});gt.GetMethod("AddCustomSize",flags).Invoke(group,new object[]{size});labels=(string[])gt.GetMethod("GetDisplayTexts",flags).Invoke(group,null);index=System.Array.FindIndex(labels,s=>s.StartsWith(label));}if(index<0)throw new System.Exception("Cannot add test size");indices[i]=index;}
int sizeIndex=0,stateIndex=0,phase=0,checks=0,errors=0,lastFrame=-1;bool done=false;
int[] tabFor={0,0,0,0,1,1,1},stepFor={0,0,1,2,0,0,1},hintFor={0,3,0,0,0,3,0};
double started=UnityEditor.EditorApplication.timeSinceStartup,next=started;
UIRangeDisplay interaction=null;CraftSpaceInteractionZone zone=null;GameObject feedback=null;TMPro.TMP_Text feedbackText=null;
UnityEditor.EditorApplication.CallbackFunction tick=null;Application.LogCallback onLog=null;
System.IO.File.WriteAllText(folder+"remaining-checks.txt","Three actual fixed GameView sizes x seven tutorial presentation fixtures; then Dorm. Not human comprehension or physical input E2E.\n");
System.Action<string> log=s=>System.IO.File.AppendAllText(folder+"remaining-checks.txt",(UnityEditor.EditorApplication.timeSinceStartup-started).ToString("F2")+"s phase="+phase+" "+s+"\n");
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks++;log("PASS "+s);};
System.Func<string,object> field=n=>c.GetType().GetField(n,flags).GetValue(c);
System.Action<string> capture=name=>{ScreenCapture.CaptureScreenshot(folder+"verified-"+name+".png");log("CAPTURE "+name+" screen="+Screen.width+"x"+Screen.height);};
System.Action inspect=()=>{
 Canvas.ForceUpdateCanvases();var panel=(RectTransform)field("messagePanel");var canvas=(RectTransform)panel.GetComponentInParent<Canvas>().transform;
 var b=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,panel);check(b.min.x>=canvas.rect.xMin-.5f && b.max.x<=canvas.rect.xMax+.5f && b.min.y>=canvas.rect.yMin-.5f && b.max.y<=canvas.rect.yMax+.5f,"panel within canvas "+sizeIndex+"/"+stateIndex);
 foreach(var text in panel.GetComponentsInChildren<TMPro.TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing&&!text.isTextTruncated,"text fits "+text.name+" "+text.text.Replace("\n"," / "));}
 var rects=new System.Collections.Generic.List<Rect>();foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>()){var r=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,button.transform);var rect=new Rect(r.min.x,r.min.y,r.size.x,r.size.y);check(rect.xMin>=panel.rect.xMin-.5f && rect.xMax<=panel.rect.xMax+.5f,"button within panel "+button.name);foreach(var prior in rects)check(!rect.Overlaps(prior),"buttons separate");rects.Add(rect);}
};
System.Action<CraftSpaceInteractionAction> enter=action=>{
 zone=null;foreach(var z in UnityEngine.Object.FindObjectsByType<CraftSpaceInteractionZone>(FindObjectsSortMode.None))if(z.ActionType==action){zone=z;break;}
 check(zone!=null,"equipment trigger exists "+action);var position=zone.GetComponent<Collider2D>().bounds.center;var col=interaction.player.GetComponent<Collider2D>();if(col!=null)position-=col.bounds.center-interaction.player.position;position.z=interaction.player.position.z;
 var body=interaction.player.GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=Vector2.zero;interaction.player.position=position;Physics2D.SyncTransforms();
};
System.Action assertGuide=()=>{check((CraftSpaceInteractionZone)typeof(UIRangeDisplay).GetField("activeZone",flags).GetValue(interaction)==zone,"real physics trigger registered");check(feedback.activeInHierarchy,"context guide visible");feedbackText.ForceMeshUpdate();check(!feedbackText.isTextOverflowing&&!feedbackText.isTextTruncated,"equipment text fits");};
System.Action<bool,string> finish=(passed,message)=>{
 if(done)return;done=true;UnityEditor.EditorApplication.update-=tick;Application.logMessageReceived-=onLog;UnityEditor.SessionState.SetBool(prefix+"remainingRunning",false);
 log((passed?"COMPLETE ":"FAILED ")+message+" checks="+checks+" errors="+errors);
 try{
  foreach(var controller in UnityEngine.Object.FindObjectsByType<GearCraft.Scripts.Craft.CraftTutorialController>(FindObjectsInactive.Include,FindObjectsSortMode.None))controller.OnCraftSceneLeaving();
  foreach(var row in prefs){var p=row.Split('|');if(!bool.Parse(p[1]))PlayerPrefs.DeleteKey(p[0]);else if(p[2]=="string")PlayerPrefs.SetString(p[0],System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(p[3])));else PlayerPrefs.SetInt(p[0],int.Parse(p[3]));}PlayerPrefs.Save();
  foreach(var row in prefs){var p=row.Split('|');bool exists=bool.Parse(p[1]);bool match=PlayerPrefs.HasKey(p[0])==exists;if(exists)match &= p[2]=="string"?PlayerPrefs.GetString(p[0])==System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(p[3])):PlayerPrefs.GetInt(p[0])==int.Parse(p[3]);if(!match)throw new System.Exception("Preference restore mismatch "+p[0]);}
  typeof(GearCraft.Gallery.GalleryProgress).GetField("data",flags).SetValue(null,null);
  vt.GetProperty("selectedSizeIndex",flags).SetValue(view,originalIndex,null);var labels=(string[])gt.GetMethod("GetDisplayTexts",flags).Invoke(group,null);for(int i=labels.Length-1;i>=0;i--)if(labels[i].StartsWith("UXTutorialOnboarding"))gt.GetMethod("RemoveCustomSize",flags).Invoke(group,new object[]{i});
  var restored=(string[])gt.GetMethod("GetDisplayTexts",flags).Invoke(group,null);if(!restored.SequenceEqual(originalLabels))throw new System.Exception("GameView list restore mismatch");
  UnityEditor.SessionState.SetBool(prefix+"backupReady",false);UnityEditor.SessionState.SetBool(prefix+"remainingPassed",passed);UnityEditor.SessionState.SetInt(prefix+"remainingChecks",checks);
  System.IO.File.WriteAllText(folder+"remaining-cleanup.txt","8 original preferences and original GameView choices restored/verified before exiting Play. Guidance closed; parent must independently verify after Play exit.\n");
  log("CLEANUP original preferences/GameView verified; exit Play to original editor Title");UnityEditor.EditorApplication.isPlaying=false;
 }catch(System.Exception ex){log("CLEANUP FAILED "+ex);}
};
onLog=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;log("ERROR "+message+"\n"+stack);}};
tick=()=>{try{
 double now=UnityEditor.EditorApplication.timeSinceStartup;if(now-started>80)throw new System.Exception("80s watchdog");if(!UnityEditor.EditorApplication.isPlaying||UnityEditor.EditorApplication.isPaused)throw new System.Exception("Play stopped/paused");if(errors>0)throw new System.Exception("Runtime errors");if(now<next||lastFrame==Time.frameCount)return;next=now+.25;lastFrame=Time.frameCount;
 switch(phase){
 case 0:vt.GetProperty("selectedSizeIndex",flags).SetValue(view,indices[sizeIndex],null);((UnityEditor.EditorWindow)view).Focus();((UnityEditor.EditorWindow)view).Repaint();phase=1;next=now+.5;break;
 case 1:
  TooltipSystem.FindInstance().Hide();c.OnCraftSceneLeaving();status.CraftTutorials.Reset();status.module_scrap=false;status.punkDrive=false;materials.SetAllMaterials(20);
  PlayerPrefs.SetInt(CraftTutorialMetrics.CraftMasteryKey,1);PlayerPrefs.SetInt(CraftTutorialMetrics.WeaponMasteryKey,1);tabs.SwitchTab(tabFor[stateIndex]);
  if(tabFor[stateIndex]==0)craft.TrySelectRecipe(craft.FindUnlockedRecipe(stateIndex==0?"GearCraft":"ScrapModule"));
  var track=tabFor[stateIndex]==0?status.CraftTutorials.craft:status.CraftTutorials.weaponCustom;track.stepIndex=stepFor[stateIndex];track.hintLevel=hintFor[stateIndex];track.usedHint=hintFor[stateIndex]>0;
  PlayerPrefs.SetInt(tabFor[stateIndex]==0?CraftTutorialMetrics.CraftMasteryKey:CraftTutorialMetrics.WeaponMasteryKey,0);c.GetType().GetMethod("StartTutorial",flags).Invoke(c,new object[]{tabFor[stateIndex]});if(stateIndex==5)c.GetType().GetMethod("TogglePracticeView",flags).Invoke(c,null);phase=2;break;
 case 2:inspect();if(stepFor[stateIndex]==2 || stateIndex==6)check(((System.Collections.Generic.List<Rect>)field("highlightBounds")).Count==0,"independent fixture has no target highlight");capture("layout-"+sizeIndex+"-"+stateIndex);phase=3;break;
 case 3:stateIndex++;if(stateIndex==7){stateIndex=0;sizeIndex++;if(sizeIndex==3){phase=4;break;}phase=0;}else phase=1;break;
 case 4:
  c.OnCraftSceneLeaving();status.DormTasks.Reset();status.UseCraftSpacebuff=false;status.HP=90;status.GATE=70;StoryPlaybackRequest.SetReturnScene("CraftSpace",true);UnityEngine.SceneManagement.SceneManager.LoadScene("CraftSpace");phase=5;next=now+.5;break;
 case 5:if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="CraftSpace" || !ArrivalMessagePresenter.IsMessagePlaying)return;capture("dorm-arrival");phase=6;break;
 case 6:
  if(ArrivalMessagePresenter.IsMessagePlaying||ArrivalMessagePresenter.IsCameraPresentationPlaying)return;
  var task=UnityEngine.Object.FindFirstObjectByType<CraftSpaceTaskUI>();check(task!=null,"dorm task UI exists");var arrow=(RectTransform)typeof(CraftSpaceTaskUI).GetField("bedGuideArrow",flags).GetValue(task);check(arrow.gameObject.activeInHierarchy,"bed guide appears after dialogue/camera finish");
  var panel=(RectTransform)typeof(CraftSpaceTaskUI).GetField("panel",flags).GetValue(task);var rowText=panel.GetComponentsInChildren<TMPro.TMP_Text>()[0];rowText.ForceMeshUpdate();check(rowText.text.Contains("A / D") && rowText.text.Contains("Space"),"first task keeps movement and bed keys");check(!rowText.isTextOverflowing&&!rowText.isTextTruncated,"dorm task text fits");capture("dorm-bed-guide");phase=7;break;
 case 7:interaction=UnityEngine.Object.FindFirstObjectByType<UIRangeDisplay>();check(interaction!=null,"dorm interaction manager ready");feedback=(GameObject)typeof(UIRangeDisplay).GetField("feedbackRoot",flags).GetValue(interaction);feedbackText=(TMPro.TMP_Text)typeof(UIRangeDisplay).GetField("feedbackText",flags).GetValue(interaction);enter(CraftSpaceInteractionAction.GateRepair);phase=8;break;
 case 8:assertGuide();check(feedbackText.text.Contains("50") && feedbackText.text.Contains("一晩に一つ") && feedbackText.text.Contains("Space"),"repair guide provides effect, limit and key");capture("dorm-repair-guide");phase=9;break;
 case 9:typeof(UIRangeDisplay).GetMethod("ExecuteActiveAction",flags).Invoke(interaction,null);phase=10;break;
 case 10:check(status.GATE==120 && status.UseCraftSpacebuff,"registered equipment action adds 50 and consumes nightly choice");check(feedbackText.text.Contains("修理しました"),"result replaces decision guide");capture("dorm-repair-result");phase=11;break;
 case 11:status.UseCraftSpacebuff=false;interaction.uplimit=1;status.HP=100;enter(CraftSpaceInteractionAction.Medical);phase=12;break;
 case 12:assertGuide();check(feedbackText.text.Contains("十分") && feedbackText.text.Contains("食事"),"full-health guide offers useful alternatives before action");capture("dorm-full-health");phase=13;break;
 case 13:UnityEngine.Object.FindFirstObjectByType<UIHotkeyController>().ToggleMenu();phase=14;break;
 case 14:check(!feedback.activeSelf,"menu hides equipment guide");capture("dorm-menu");phase=15;break;
 case 15:UnityEngine.Object.FindFirstObjectByType<UIHotkeyController>().CloseMenu();phase=16;break;
 case 16:assertGuide();check(feedbackText.text.Contains("十分"),"closing menu restores relevant context guide");finish(true,"21 layout fixtures and dorm context checks passed");break;
 }
}catch(System.Exception ex){finish(false,ex.ToString());}};
Application.logMessageReceived+=onLog;UnityEditor.SessionState.SetBool(prefix+"remainingRunning",true);UnityEditor.EditorApplication.update+=tick;return "Scheduled remaining checks with automatic restoration and Play exit";

} }
