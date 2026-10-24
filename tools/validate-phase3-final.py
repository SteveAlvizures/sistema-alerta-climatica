"""E2E with test-data writes: creates test accounts and domain records. Use an isolated stack."""
import json,os,secrets,time,urllib.request,urllib.error,sys
from pathlib import Path
sys.stdout.reconfigure(encoding='utf8')
Path('tmp').mkdir(exist_ok=True)
base=os.environ['PHASE3_E2E_URL'].rstrip('/')
if os.environ.get('PHASE3_E2E_ALLOW_WRITES')!='true':
 raise SystemExit('Set PHASE3_E2E_ALLOW_WRITES=true only for an isolated validation stack.')
values={'INITIAL_ADMIN_USERNAME':os.environ['PHASE3_E2E_USERNAME'],'INITIAL_ADMIN_PASSWORD':os.environ['PHASE3_E2E_PASSWORD']}
evidence=[]
def call(path,expected=200,method='GET',body=None,token=None,headers=None):
 h={'Content-Type':'application/json',**(headers or {})}
 if token:h['Authorization']='Bearer '+token
 req=urllib.request.Request(base+path,headers=h,method=method,data=json.dumps(body).encode() if body is not None else None)
 try:
  with urllib.request.urlopen(req,timeout=45) as r:code=r.status;data=r.read()
 except urllib.error.HTTPError as r:code=r.code;data=r.read()
 evidence.append({'method':method,'path':path,'status':code})
 if code!=expected:
  Path('tmp/final-e2e-failure.json').write_bytes(data)
  raise AssertionError((path,expected,code))
 if not data:return None
 try:return json.loads(data)
 except ValueError:return None
admin=call('/api/auth/login',method='POST',body={'username':values['INITIAL_ADMIN_USERNAME'],'password':values['INITIAL_ADMIN_PASSWORD']})['accessToken']
call('/health/live');call('/health/ready')
communities=call('/api/communities');sensors=call('/api/sensors')
assert isinstance(communities,list) and isinstance(sensors,list)
assert call('/api/sensor-readings?page=1&pageSize=1')['totalCount']>=0
assert call('/api/sensor-readings/latest?page=1&pageSize=50')['totalCount']>=0
assert call('/api/alerts?page=1&pageSize=50')['totalCount']>=0
assert isinstance(call('/api/alert-rules'),list)
assert call('/api/events?page=1&pageSize=50',token=admin)['totalCount']>=0
call('/api/audit-actions?page=1&pageSize=10',token=admin)
if communities:call('/api/dashboard?communityId='+communities[0]['id'])
call('/api/audit-actions',401);call('/api/reports/types',401)
tokens={}
for role in ['Operator','ConsultationUser']:
 username='final-'+role.lower()+'-'+secrets.token_hex(4);password=secrets.token_urlsafe(24)
 call('/api/users',201,'POST',{'name':'Final validation '+role,'username':username,'password':password,'role':role},admin)
 tokens[role]=call('/api/auth/login',method='POST',body={'username':username,'password':password})['accessToken']
operator=tokens['Operator'];reader=tokens['ConsultationUser']
for token in [operator,reader]:
 call('/api/events?page=1&pageSize=10',token=token);call('/api/audit-actions',403,token=token)
call('/api/reports/types',token=operator);call('/api/reports/types',403,token=reader)
payload={'name':'ZZ-Final-'+secrets.token_hex(4),'location':'Validation area','description':'Isolated HTTP end-to-end',
 'municipality':'Town','department':'Region','country':'Guatemala','latitude':14.6,'longitude':-90.5,'isActive':True}
call('/api/communities',401,'POST',payload)
call('/api/communities',403,'POST',payload,reader)
community=call('/api/communities',201,'POST',payload,operator)
call('/api/communities',409,'POST',payload,admin)
call('/api/communities',400,'POST',{**payload,'name':'Bad coordinates','latitude':91},admin)
call('/api/communities/'+str(__import__('uuid').uuid4()),404)
sensor=call('/api/sensors',201,'POST',{'communityId':community['id'],'measurementType':'Temperature','type':'Temperature',
 'name':'Validation temperature','code':'FINAL-'+secrets.token_hex(4),'location':'Validation area','unit':'°C','isActive':True},operator)
call('/api/communities/'+community['id']+'/sensors')
call('/api/sensors/'+sensor['id']+'/readings/latest',404)
rule=call('/api/alert-rules',201,'POST',{'communityId':community['id'],'sensorId':sensor['id'],'name':'Validation heat',
 'code':'FINAL-'+secrets.token_hex(4),'phenomenon':'Drought','variable':'Temperature','dangerLevel':'Red','minValue':30,'maxValue':40,
 'validFrom':'2020-01-01T00:00:00Z','message':'Validation threshold','isActive':True},operator)
call('/api/sensor-readings',201,'POST',{'sensorId':sensor['id'],'value':35.5},operator)
for attempt in range(30):
 rows=call('/api/alerts?communityId='+community['id']+'&page=1&pageSize=50')['data']
 if rows:break
 time.sleep(1)
else:raise AssertionError('Sensor outbox did not generate alert')
alert=rows[0];assert alert['unit']=='°C' and alert['sensorName']==sensor['name']
call('/api/alerts/'+alert['id']+'/resolve',409,'PATCH',{},operator)
call('/api/alerts/'+alert['id']+'/acknowledge',200,'PATCH',{},operator)
call('/api/alerts/'+alert['id']+'/resolve',200,'PATCH',{},operator)
assert alert['eventId'] is not None
event=call('/api/events/'+alert['eventId'],token=reader)
assert event['event']['status']=='Closed'
call('/api/events/statistics?communityId='+community['id'],token=reader)
call('/api/dashboard?communityId='+community['id'])
call('/api/communities/'+community['id'],409,'DELETE',token=admin)
call('/api/sensor-readings?page=0',400)
call('/api/reports',400,'POST',{'type':'readings','filters':{'dateFrom':'2026-10-24T00:00:00Z','dateTo':'2026-10-23T00:00:00Z'}},admin)
for action,resource_id in [('ComunidadCreada',community['id']),('SensorCreado',sensor['id']),('ReglaCreada',rule['id']),('AlertaCerrada',alert['id'])]:
 for attempt in range(20):
  audit=call('/api/audit-actions?action='+action+'&pageSize=100',token=admin)
  if any(row['affectedRecordId']==resource_id for row in audit['data']):break
  time.sleep(1)
 else:raise AssertionError('Domain audit delivery missing: '+action)
call('/api/auth/logout',204,'POST',{},operator)
call('/internal/identity/current',404,token=admin)
Path('tmp/final-e2e-results.json').write_text(json.dumps(evidence,indent=2))
print('Final integrated E2E:',len(evidence),'HTTP checks passed; 3 roles; durable reading -> alert -> event closure verified.')
