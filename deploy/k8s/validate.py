"""Offline contract validation. Does not read .env, kubeconfig or any live Secret."""
from pathlib import Path
import sys
import yaml

root=Path(__file__).resolve().parent
docs=[]
for path in root.glob('*.yaml'):
    if path.name in ('secret.example.yaml','kustomization.yaml'):continue
    docs.extend(d for d in yaml.safe_load_all(path.read_text(encoding='utf-8')) if d)
by={(d['kind'],d['metadata']['name']):d for d in docs}
namespace='vigiarural-phase4'
for d in docs:
    if d['kind']!='Namespace':assert d['metadata']['namespace']==namespace
    assert d['kind']!='Secret','Real Secrets are supplied privately'
config=by['ConfigMap','vigiarural-config']['data']
keys=set(yaml.safe_load((root/'secret.example.yaml').read_text())['stringData'])
deploys={n:d for (kind,n),d in by.items() if kind=='Deployment'}
apps=['frontend','gateway','auth-service','community-service','sensor-service','alert-service','report-service','audit-service','backend']
databases=['auth-postgres','community-postgres','alert-postgres','sensor-sqlserver','sqlserver','report-mongodb','audit-mongodb']
assert set(deploys)==set(apps+databases)
for name,d in deploys.items():
    pod=d['spec']['template']['spec']
    assert pod['automountServiceAccountToken'] is False
    for c in pod['containers']:
        assert all(k in c for k in ('readinessProbe','livenessProbe','resources'))
        for kind in ['readinessProbe','livenessProbe']:
            assert c[kind].get('httpGet',{}).get('path')!='/','No SPA fallback health probes'
        for e in c.get('env',[]):
            assert e['name']!='JWT_SECRET'
            ref=e.get('valueFrom',{})
            if 'secretKeyRef' in ref:assert ref['secretKeyRef']['key'] in keys
            if 'configMapKeyRef' in ref:assert ref['configMapKeyRef']['key'] in config
    for v in pod.get('volumes',[]):
        if 'persistentVolumeClaim' in v:assert ('PersistentVolumeClaim',v['persistentVolumeClaim']['claimName']) in by
        if 'configMap' in v:assert ('ConfigMap',v['configMap']['name']) in by
    if name in databases:
        assert d['spec']['replicas']==1 and d['spec']['strategy']['type']=='Recreate'
    else:assert ('Service',name) in by
def envs(name):return {e['name']:e.get('value',e.get('valueFrom')) for e in deploys[name]['spec']['template']['spec']['containers'][0]['env']}
g=envs('gateway')
assert g['ASPNETCORE_URLS']=='http://127.0.0.1:8080'
assert g['Gateway__TrustedProxyAddresses__0']=='127.0.0.1'
assert len([k for k in g if k.startswith('Gateway__TrustedProxyAddresses__')])==1
assert g['ReverseProxy__Clusters__sensor__Destinations__primary__Address']=='http://sensor-service:8080/'
assert g['ReverseProxy__Clusters__monolith__Destinations__backend__Address']=='http://backend:8080/'
assert g['ReverseProxy__Clusters__reports__Destinations__report-service__Address']=='http://report-service:8000/'
assert envs('backend')['DASHBOARD_SENSOR_URL']=='http://sensor-service:8080'
assert deploys['sensor-service']['spec']['replicas']==1
assert deploys['sensor-service']['spec']['strategy']['type']=='Recreate'
assert config['OAUTH_PUBLIC_ORIGIN']=='https://vigiarural.site' and config['OAUTH_ENV']=='production'
ing=by['Ingress','vigiarural-phase4']['spec'];assert ing['tls'][0]['secretName']=='vigiarural-public-tls'
routes={p['path']:p['backend']['service']['name'] for p in ing['rules'][0]['http']['paths']}
assert routes=={'/api':'gateway','/health':'gateway','/':'frontend'}
assert by['ServersTransport','gateway-tls']['spec']=={'serverName':'vigiarural.site'}
gateway_nginx=by['ConfigMap','gateway-nginx']['data']['default.conf']
assert 'proxy_set_header X-Forwarded-Proto $scheme;' in gateway_nginx
assert 'ssl_protocols TLSv1.2 TLSv1.3;' in gateway_nginx
assert 'proxy_ssl_verify on;' in by['ConfigMap','frontend-nginx']['data']['default.conf']
assert ('NetworkPolicy','sensor-service') in by
assert ('HorizontalPodAutoscaler','sensor-service') not in by
print(f'PASS: {len(docs)} objects; 9 applications; 7 persistent databases; routes, health, secrets references and TLS trust contracts validated')
