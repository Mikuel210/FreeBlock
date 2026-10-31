"""Destructive Linux smoke test. Use ONLY a disposable clean FreeBlock installation.
Leaves test locks and an active schedule behind; reset the disposable daemon afterwards.
Run with python3 Tests/command_smoke.py. Uses reserved .invalid domains.
"""
import subprocess, os, json, datetime, pathlib, time
out=[]
editor=pathlib.Path('/home/hermes/.hermes/cache/scratch/freeblock-editor')
editor.write_text('#!/usr/bin/python3\nimport os,sys\nfrom pathlib import Path\nPath(sys.argv[1]).write_text(os.environ["FB_ENTRIES"])\n');editor.chmod(0o755)
def run(*args, entries='list-a.invalid\n', expected=None, code=0):
 env=dict(os.environ,EDITOR=str(editor),FB_ENTRIES=entries)
 p=subprocess.run(['freeblock',*args],input='yes\n'*20,text=True,capture_output=True,env=env,timeout=20)
 text=p.stdout+p.stderr
 ok=p.returncode==code and 'Connection refused' not in text and 'HubException' not in text and (expected is None or expected in text)
 out.append(dict(args=args,code=p.returncode,ok=ok,output=text))
 print(('PASS ' if ok else 'FAIL ')+' '.join(args)+'\n'+text,flush=True)
 pathlib.Path('/home/hermes/.hermes/cache/scratch/freeblock-command-results.json').write_text(json.dumps(out,indent=2))
 if not ok: raise SystemExit('command check failed')
 return text
run('--version');run('--help');run();
for group in ['list','lock','schedule','config']: run(group)
run('status');run('block','manual-a.invalid','manual-b.invalid');run('status',expected='manual-a.invalid');run('unblock','manual-a.invalid','manual-b.invalid')
run('list','add','reg-list');run('list','status',expected='reg-list');run('list','show','reg-list',expected='list-a.invalid')
run('list','edit','reg-list',entries='list-b.invalid\n');run('list','show','reg-list',expected='list-b.invalid')
run('list','rename','reg-list','reg-list-new');run('block','@reg-list-new');run('status',expected='reg-list-new');run('unblock','@reg-list-new');run('list','remove','reg-list-new')
run('lock','add','reg-lock','00:00:30','lock-a.invalid');run('lock','status',expected='reg-lock');run('lock','show','reg-lock',expected='lock-a.invalid');run('lock','edit','reg-lock','--add-entries','lock-b.invalid','--extend','00:00:01');run('lock','show','reg-lock',expected='lock-b.invalid');run('lock','rename','reg-lock','reg-lock-new');run('lock','show','reg-lock-new',expected='reg-lock-new')
daychars='MTWHFSU'; today=datetime.datetime.now().weekday(); tomorrow=daychars[(today+1)%7]
run('schedule','add','reg-schedule','12:00','13:00',tomorrow,'schedule-a.invalid');run('schedule','status',expected='reg-schedule');run('schedule','show','reg-schedule',expected='schedule-a.invalid');run('schedule','edit','reg-schedule','--start-time','11:00','--end-time','14:00','--days',tomorrow,'--entries','schedule-b.invalid','--warning-time','00:20');run('schedule','show','reg-schedule',expected='schedule-b.invalid');run('schedule','rename','reg-schedule','reg-schedule-new');run('schedule','remove','reg-schedule-new')
now=datetime.datetime.now(); start=(now-datetime.timedelta(hours=1)).strftime('%H:%M'); end=(now+datetime.timedelta(hours=1)).strftime('%H:%M')
run('schedule','add','reg-active',start,end,'MTWHFSU','active.invalid');run('schedule','remove','reg-active',expected='Requested schedule removal');run('schedule','show','reg-active',expected='removal window');run('schedule','remove','reg-active',expected='already requested',code=1)
run('config','status')
for key,value,display in [('status.showAllLists','true','True'),('status.showAllSchedules','true','True'),('status.showWarningPeriodSchedules','false','False'),('schedule.defaultWarningTime','00:23:00','00:23:00')]:
 run('config','set',key,value);run('config','get',key,expected=display)
print('ALL COMMAND CASES PASSED',len(out))