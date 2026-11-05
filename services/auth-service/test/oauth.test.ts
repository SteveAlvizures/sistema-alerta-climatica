import 'reflect-metadata';
import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { OAuthService, oauthOrigin, validOpaque } from '../src/auth/oauth.service';
import { canonicalRole, normalizeRole } from '../src/identity/roles';
import { Tokens, ROLE_CLAIM } from '../src/auth/tokens';
import { OAuth2Client } from 'google-auth-library';

const originalFetch=globalThis.fetch;
afterEach(()=>{globalThis.fetch=originalFetch;});
for(const [input,official,legacy] of [['ADMIN','ADMIN','Administrator'],['Administrator','ADMIN','Administrator'],['OPERATOR','OPERATOR','Operator'],['Operator','OPERATOR','Operator'],['VIEWER','VIEWER','ConsultationUser'],['User','VIEWER','ConsultationUser']]) {
  test('compatible role '+input,()=>{assert.equal(canonicalRole(input),official);assert.equal(normalizeRole(input),legacy);});
}
test('official JWT preserves the single SHA256 signing contract and legacy URI role',()=>{
  process.env.JWT_KEY='fixture-only-signing-key-at-least-32-characters';
  const tokens=new Tokens(),claims=tokens.verify(tokens.issue({id:'ad274c89-61d2-4b85-bbd9-2a02c35d5645',name:'Admin',username:'admin',role:'ADMIN'}).accessToken);
  assert.equal(claims.role,'ADMIN');assert.equal(claims[ROLE_CLAIM],'Administrator');
});
test('production config rejects LAN and localhost and allows only the final origin',()=>{
  process.env.OAUTH_ENV='production';
  for(const origin of ['http://localhost:4200','https://172.20.10.4:18444','https://evil.test','https://vigiarural.site/other']) {
    process.env.OAUTH_PUBLIC_ORIGIN=origin;assert.throws(oauthOrigin);
  }
  process.env.OAUTH_PUBLIC_ORIGIN='https://vigiarural.site';assert.equal(oauthOrigin(),'https://vigiarural.site');
});
test('opaque codes reject arrays, short values and malformed values',()=>{
  for(const input of [null,undefined,[],['a'.repeat(43)],'short','!'.repeat(43)]) assert.equal(validOpaque(input),false);
});
test('GitHub never trusts public email without verified email API metadata',async()=>{
  process.env.GITHUB_CLIENT_ID='test';process.env.GITHUB_CLIENT_SECRET='test';
  const urls:string[]=[];
  globalThis.fetch=(async(input)=>{
    const url=String(input);urls.push(url);
    return new Response(JSON.stringify(url.includes('access_token')?{access_token:'test'}:url.endsWith('/emails')?[{email:'admin@example.test',verified:false,primary:true}]:{id:123,email:'admin@example.test',login:'test'}),{status:200});
  }) as typeof fetch;
  const oauth=new OAuthService(null as any,null as any,null as any);
  await assert.rejects(()=>oauth.providerIdentity('github','code','verifier','nonce','https://vigiarural.site'));
  assert.ok(urls.includes('https://api.github.com/user/emails'));
});
test('GitHub selects a verified email instead of the unverified public one',async()=>{
  globalThis.fetch=(async(input)=>new Response(JSON.stringify(String(input).includes('access_token')?{access_token:'test'}:String(input).endsWith('/emails')?[{email:'verified@example.test',verified:true,primary:true}]:{id:123,email:'unverified@example.test',login:'test'}),{status:200})) as typeof fetch;
  const result=await new OAuthService(null as any,null as any,null as any).providerIdentity('github','code','verifier','nonce','https://vigiarural.site');
  assert.equal(result.email,'verified@example.test');assert.equal(result.subject,'123');assert.equal(result.verified,true);
});
test('Google requires verified email and matching OIDC nonce after library verification',async()=>{
  process.env.GOOGLE_CLIENT_ID='test';process.env.GOOGLE_CLIENT_SECRET='test';
  globalThis.fetch=(async()=>new Response(JSON.stringify({id_token:'test'}))) as typeof fetch;
  const original=OAuth2Client.prototype.verifyIdToken;
  let payload:any={sub:'google-sub',email:'verified@example.test',email_verified:true,nonce:'expected'};
  OAuth2Client.prototype.verifyIdToken=(async()=>({getPayload:()=>payload})) as any;
  try {
    const oauth=new OAuthService(null as any,null as any,null as any);
    assert.equal((await oauth.providerIdentity('google','code','verifier','expected','https://vigiarural.site')).subject,'google-sub');
    await assert.rejects(()=>oauth.providerIdentity('google','code','verifier','wrong','https://vigiarural.site'));
    payload={...payload,email_verified:false};await assert.rejects(()=>oauth.providerIdentity('google','code','verifier','expected','https://vigiarural.site'));
    OAuth2Client.prototype.verifyIdToken=(async()=>{throw new Error('bad signature');}) as any;
    await assert.rejects(()=>oauth.providerIdentity('google','code','verifier','expected','https://vigiarural.site'));
  } finally {OAuth2Client.prototype.verifyIdToken=original;}
});
