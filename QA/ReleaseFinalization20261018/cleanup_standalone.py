from pathlib import Path
import importlib.util,json,os,signal,subprocess,time,hashlib
qa=Path(__file__).resolve().parent
run=qa/'standalone-smoke-run1'
spec=importlib.util.spec_from_file_location('smoke',qa/'standalone_smoke.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
launched=json.loads((run/'launched-process.json').read_text())
pid=launched['pid'];expected=launched['command'][0]
assert pid==30457 and expected==str(m.APP/'Contents/MacOS/GearCraft_Remake')
assert not (run/'cleanup-result.json').exists()
meta=json.loads((run/'player-domain-before.json').read_text())
import plistlib
before=plistlib.loads((run/'player-domain-before.export.plist').read_bytes())
assert m.digest(m.canonical(before))==meta['typed_sha256'] and meta['exists']
result={'pid':pid,'normal_exit_test':'FAILED_TIMEOUT','signals':[],
 'prior_cleanup_attempt':{'signal':'SIGTERM','delivered':True,
 'observation':'Process disappeared after signal; intervening identity check aborted safely before preference writes.'}}
def process():
 # Put the executable last and disable column truncation before identity checks.
 r=subprocess.run(['/bin/ps','-ww','-p',str(pid),'-o','stat=','-o','comm='],text=True,capture_output=True)
 s=r.stdout.strip()
 if r.returncode or not s:return None
 parts=s.split(None,1)
 if 'Z' in parts[0]:return None
 assert len(parts)==2 and parts[1].strip()==expected,'PID identity changed'
 return s
if process() is not None:
 for sig in (signal.SIGTERM,signal.SIGKILL):
  os.kill(pid,sig);result['signals'].append(sig.name)
  print(json.dumps({'cleanup_signal':sig.name,'pid':pid}),flush=True)
  until=time.monotonic()+8
  while time.monotonic()<until and process() is not None:time.sleep(.25)
  if process() is None:break
assert process() is None,'Owned test process still running; leave backup for review'
result['process_stopped']=True
md,raw,export,after=m.domain_snapshot(m.PLAYER_DOMAIN)
if not (run/'cleanup-domain-before-restore.json').exists():
 m.dump_new(run/'cleanup-domain-before-restore.json',md)
 m.write_new(run/'cleanup-domain-before-restore.export.plist',export)
# defaults import merges keys: remove only the test-created key, whose value is
# unchanged from the post-exit snapshot, before restoring the original domain.
post_exit=plistlib.loads((run/'cleanup-domain-before-restore.export.plist').read_bytes())
extras=set(after)-set(before)
assert extras <= {'GearCraft.Gallery.Progress.v1'},'Unexpected added key; manual review required'
for key in sorted(extras):
 assert key in post_exit and m.canonical(after[key])==m.canonical(post_exit[key]),'Added key changed since test exit'
 r=subprocess.run(['/usr/bin/defaults','delete',m.PLAYER_DOMAIN,key],capture_output=True)
 assert r.returncode==0,'Test-created key restoration failed'
result['test_created_keys_removed']=sorted(extras)
r=subprocess.run(['/usr/bin/defaults','import',m.PLAYER_DOMAIN,str(run/'player-domain-before.export.plist')],capture_output=True)
assert r.returncode==0,'Full domain restoration failed'
for attempt in range(20):
 restored,raw,export,values=m.domain_snapshot(m.PLAYER_DOMAIN)
 if m.canonical(values)==m.canonical(before) and restored['exists']==meta['exists'] and restored['disk_matches_export'] is True:break
 time.sleep(.25)
else:raise RuntimeError('Domain readback differs')
m.write_new(run/'cleanup-domain-restored.export.plist',export)
result['full_player_domain_restored']=True;result['keys_restored']=len(before)
protected=json.loads((run/'protected-domains-before.json').read_text())
result['protected_domains_unchanged']={}
for domain,old in protected.items():
 now=m.domain_snapshot(domain)[0]
 result['protected_domains_unchanged'][domain]=(now['exists']==old['exists'] and now['typed_sha256']==old['typed_sha256'])
result['application_support_changes']=m.file_changes(json.loads((run/'application-support-before.json').read_text()),m.support_manifest())
result['utc']=m.utc()
m.dump_new(run/'cleanup-result.json',result)
print(json.dumps(result),flush=True)
