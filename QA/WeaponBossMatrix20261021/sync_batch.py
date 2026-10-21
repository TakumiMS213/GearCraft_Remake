from pathlib import Path
import hashlib,re
q=Path(__file__).resolve().parent
p=q/'BatchDirect.cs.txt';s=p.read_text()
b=(q/'NativeInputBootstrap.cs.txt').read_text().rstrip()
b=b.replace('UnityEditor.EditorApplication.isPlaying=false;','Time.timeScale=0f; /* Batch owns Play teardown. */')
b=b.replace('try{\n // ResetRunProgress', 'bBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};\ntry{\n // ResetRunProgress',1)
a=s.index('System.Func<string> bRunBootstrap=()=>{\n')+len('System.Func<string> bRunBootstrap=()=>{\n');z=s.index('\n};\nSystem.Func<string> bRunRoute=',a);s=s[:a]+b+'\n'+s[z:]
r=(q/'NativeInputSurvivalRoute.cs.txt').read_text().rstrip()
r=r.replace('UnityEditor.EditorApplication.isPlaying=false;','Time.timeScale=0f; /* Batch owns Play teardown. */')
r=r.replace('System.Func<BonusCardDataSO,float> cardScore=data=>{','bRouteFinish=finish;\nSystem.Func<BonusCardDataSO,float> cardScore=data=>{',1)
a=s.index('System.Func<string> bRunRoute=()=>{\n')+len('System.Func<string> bRunRoute=()=>{\n');z=s.index('\n};\ndouble bStartedAt=',a);s=s[:a]+r+'\n'+s[z:]
for key,file in [('bFolder+"NativeInputBootstrap.cs.txt"','NativeInputBootstrap.cs.txt'),('bFolder+"NativeInputSurvivalRoute.cs.txt"','NativeInputSurvivalRoute.cs.txt'),('bFolder+bPlanFile','direct-cases.csv')]:
 digest=hashlib.sha256((q/file).read_bytes()).hexdigest()
 s,n=re.subn(r'bHash\('+re.escape(key)+r'\)!="[0-9a-f]{64}"','bHash('+key+')!="'+digest+'"',s)
 assert n==1,(key,n)
p.write_text(s)
print('Embedded current bootstrap/route; refreshed all three SHA checks.')
