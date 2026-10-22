import 'reflect-metadata';
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { INestApplication } from '@nestjs/common';
import { createApp } from '../src/main';
import { Database } from '../src/persistence/database';
import { importUsers } from '../src/persistence/import-users';
import { IdentityPasswords } from '../src/auth/passwords';

const enabled=process.env.AUTH_INTEGRATION_TESTS==='true';
let app:INestApplication,db:Database,base:string,adminToken:string;
const fixture=JSON.parse(readFileSync('test/fixtures/generated.json','utf8'));
const readerId=randomUUID(),operatorId=randomUUID(),inactiveId=randomUUID();
const rows=[
  {id:fixture.userId,name:'Admin original',email:'fixture-admin',role:'Administrator',isActive:true},
  {id:readerId,name:'Repeated display',email:'reader',role:'User',isActive:true},
  {id:operatorId,name:'Repeated display',email:'operator@example.test',role:'Operator',isActive:true},
  {id:inactiveId,name:'Inactive',email:'inactive',role:'ConsultationUser',isActive:false},
].map(user=>({...user,passwordHash:fixture.hashes[0].hash,createdAt:'2026-08-24T20:00:00.000Z',lastAccessAt:null}));
async function call(path:string,method='GET',body?:unknown,token?:string,headers:Record<string,string>={}) {
  const response=await fetch(base+path,{method,headers:{...headers,...(body?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{})},body:body?JSON.stringify(body):undefined});
  const text=await response.text();return {status:response.status,body:text?JSON.parse(text):null,headers:response.headers};
}
async function login(username:string,password=fixture.password) {return call('/api/auth/login','POST',{username,password});}
before(async()=>{
  if(!enabled)return;
  assert.ok(process.env.AUTH_DATABASE_NAME?.endsWith('_test'),'Refusing to reset a non-test database');
  process.env.JWT_KEY='fixture-only-signing-key-at-least-32-characters';process.env.JWT_ISSUER='test';process.env.JWT_AUDIENCE='test';
  process.env.AUTH_INTERNAL_KEY='integration-only-internal-key-32-characters';process.env.AUTH_AUDIT_URL='http://127.0.0.1:1/internal/auth-events';
  db=new Database();await db.onModuleInit();await db.pool.query('TRUNCATE auth_outbox,auth_users');
  assert.deepEqual(await importUsers(db,rows),{inserted:4,unchanged:0});
  app=await createApp();await app.listen(0,'127.0.0.1');base=await app.getUrl();
  adminToken=(await login('fixture-admin')).body.accessToken;
});
after(async()=>{await app?.close();await db?.onModuleDestroy();});
const integration=(name:string,run:()=>Promise<void>)=>test(name,{skip:!enabled},run);
integration('versioned schema and repeatable import preserve hashes and IDs',async()=>{
  await db.onModuleInit();assert.deepEqual(await importUsers(db,rows),{inserted:0,unchanged:4});
  const row=(await db.pool.query('SELECT * FROM auth_users WHERE id=$1',[fixture.userId])).rows[0];
  assert.ok(row.password_hash===fixture.hashes[0].hash,'Imported hash must remain unchanged');assert.equal(row.created_at.toISOString(),rows[0].createdAt);
  await assert.rejects(()=>importUsers(db,[{...rows[0],name:'changed'}]));
});
integration('login identifier normalization and unique display-name fallback',async()=>{
  assert.equal((await login(' FIXTURE-ADMIN ')).status,200);
  assert.equal((await login('admin original')).status,200);
  assert.equal((await login(' OPERATOR@EXAMPLE.TEST ')).status,200);
  assert.equal((await login('Repeated display')).status,401);
});
for(const [username,password] of [['fixture-admin','wrong'],['missing','wrong'],['inactive',fixture.password]])
  integration(`login rejects ${username==='fixture-admin'?'incorrect password':username}`,async()=>{
    const result=await login(username,password);assert.equal(result.status,401);assert.deepEqual(result.body,{message:'Usuario o contraseña incorrectos.'});
  });
integration('User legacy role normalizes to ConsultationUser',async()=>assert.equal((await login('reader')).body.role,'ConsultationUser'));
integration('missing login fields preserve 400 while blank credentials preserve legacy 401',async()=>{
  for(const body of [{},{username:null,password:'x'}])
    assert.equal((await call('/api/auth/login','POST',body)).status,400);
  for(const body of [{username:'',password:'x'},{username:'x',password:' '},{username:'x',password:''}])
    assert.equal((await call('/api/auth/login','POST',body)).status,401);
});
integration('all user endpoints enforce 401 or 403 for non-administrators',async()=>{
  for(const identity of [null,'reader','operator@example.test']) {
    const token=identity?(await login(identity)).body.accessToken:undefined;
    for(const [path,method,body] of [
      ['/api/users','GET',undefined],['/api/users/'+readerId,'GET',undefined],
      ['/api/users','POST',{}],['/api/users/'+readerId,'PUT',{}],
      ['/api/users/'+readerId+'/role','PATCH',{role:'Administrator'}],['/api/users/'+readerId+'/status','PATCH',{isActive:false}],
    ] as [string,string,unknown][]) assert.equal((await call(path,method,body,token)).status,identity?403:401);
  }
});
integration('admin creates, edits, filters and detects duplicates without exposing password hashes',async()=>{
  const created=await call('/api/users','POST',{name:'New User',username:' NEW-USER ',password:fixture.password,role:'Operator'},adminToken);
  assert.equal(created.status,201);assert.equal(created.body.username,'new-user');assert.ok(created.headers.get('location')?.startsWith('/api/users/'));
  assert.ok(!JSON.stringify(created.body).includes('password'));
  assert.equal((await call('/api/users','POST',{name:'Duplicate',username:'new-user',password:fixture.password,role:'Operator'},adminToken)).status,409);
  assert.equal((await call('/api/users/'+created.body.id,'PUT',{name:'Edited',username:'edited',password:'ignored'},adminToken)).status,200);
  assert.equal((await login('edited')).status,200);
  const listed=await call('/api/users?search=edit&page=1&pageSize=1&role=Operator&isActive=true','GET',undefined,adminToken);
  assert.equal(listed.body.totalCount,1);assert.equal(listed.body.data[0].name,'Edited');
  assert.equal((await call('/api/users?role=ConsultationUser','GET',undefined,adminToken)).body.totalCount,2);
});
integration('invalid input produces 400, absent UUID 404, self-demotion/deactivation 409',async()=>{
  for(const body of [ {},{name:'x',username:'a b',password:fixture.password,role:'Operator'},
    {name:'x',username:'x',password:'short',role:'Operator'}, {name:'x',username:'x',password:fixture.password,role:'User'}])
    assert.equal((await call('/api/users','POST',body,adminToken)).status,400);
  assert.equal((await call('/api/users?page=0','GET',undefined,adminToken)).status,400);
  assert.equal((await call('/api/users/'+randomUUID(),'GET',undefined,adminToken)).status,404);
  assert.equal((await call('/api/users/'+fixture.userId+'/status','PATCH',{isActive:false},adminToken)).status,409);
  assert.equal((await call('/api/users/'+fixture.userId+'/role','PATCH',{role:'Operator'},adminToken)).status,409);
  assert.equal((await call('/api/users/'+readerId+'/status','PATCH',{},adminToken)).status,400);
});
integration('existing JWT immediately follows current role and active state',async()=>{
  const token=(await login('reader')).body.accessToken;
  assert.equal((await call('/api/users','GET',undefined,token)).status,403);
  assert.equal((await call('/api/users/'+readerId+'/role','PATCH',{role:'Administrator'},adminToken)).status,200);
  assert.equal((await call('/api/users','GET',undefined,token)).status,200);
  await call('/api/users/'+readerId+'/status','PATCH',{isActive:false},adminToken);
  assert.equal((await call('/api/users','GET',undefined,token)).status,401);
  assert.equal((await login('reader')).status,401);
  await call('/api/users/'+readerId+'/status','PATCH',{isActive:true},adminToken);
  await call('/api/users/'+readerId+'/role','PATCH',{role:'ConsultationUser'},adminToken);
});
integration('internal identity requires service credential AND a valid JWT',async()=>{
  assert.equal((await call('/internal/identity/current','GET',undefined,adminToken)).status,401);
  const key={'X-Internal-Service-Key':process.env.AUTH_INTERNAL_KEY!};
  assert.equal((await call('/internal/identity/current','GET',undefined,undefined,key)).status,401);
  const current=await call('/internal/identity/current','GET',undefined,adminToken,key);
  assert.equal(current.status,200);assert.equal(current.body.id,fixture.userId);assert.ok(current.body.permissions.includes('AdministratorOnly'));
  // Token came from the actual .NET JwtSecurityTokenHandler fixture, not a JS mock.
  assert.equal((await call('/internal/identity/current','GET',undefined,fixture.token,key)).status,200);
});
integration('logout is durable during audit outage and does not blacklist access tokens',async()=>{
  assert.equal((await call('/api/auth/logout','POST',{},adminToken)).status,204);
  assert.equal((await call('/api/users','GET',undefined,adminToken)).status,200);
  const events=await db.pool.query("SELECT * FROM auth_outbox WHERE payload->>'action'='Logout' AND delivered_at IS NULL");
  assert.ok(events.rowCount!>0);
});
integration('database enforces case-insensitive uniqueness and a failed import rolls back',async()=>{
  const newRow={...rows[0],id:randomUUID(),email:'another-import'};
  await assert.rejects(()=>importUsers(db,[newRow,{...rows[1],id:randomUUID(),email:'FIXTURE-ADMIN'}]));
  assert.equal((await db.pool.query('SELECT 1 FROM auth_users WHERE id=$1',[newRow.id])).rowCount,0);
});
integration('health and unknown routes do not leak stack traces',async()=>{
  for(const path of ['/health','/health/live','/health/ready'])assert.equal((await call(path)).status,200);
  const missing=await call('/api/auth/unknown');assert.equal(missing.status,404);assert.ok(!JSON.stringify(missing.body).includes('stack'));
});
integration('PostgreSQL restart keeps imported users, hashes and durable audit events',async()=>{
  assert.equal(process.env.AUTH_TEST_CONTAINER,'climate-alert-auth-tests-postgres-1');
  const before=(await db.pool.query('SELECT count(*) FROM auth_outbox')).rows[0].count;
  // Only the dedicated test PostgreSQL container may be restarted.
  execFileSync('docker',['restart',process.env.AUTH_TEST_CONTAINER!],{stdio:'ignore'});
  let ready=false;
  for(let n=0;n<40;n++) {
    try {await db.pool.query('SELECT 1');ready=true;break;}catch{await new Promise(resolve=>setTimeout(resolve,500));}
  }
  assert.ok(ready);assert.equal((await login('fixture-admin')).status,200);
  assert.ok(Number((await db.pool.query('SELECT count(*) FROM auth_outbox')).rows[0].count)>=Number(before));
});
