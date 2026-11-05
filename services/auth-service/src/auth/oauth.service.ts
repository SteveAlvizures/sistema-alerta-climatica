import { Injectable, UnauthorizedException } from '@nestjs/common';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { OAuth2Client } from 'google-auth-library';
import { Database } from '../persistence/database';
import { Tokens } from './tokens';
import { Outbox } from '../integration/outbox';
import { identityRow } from '../identity/identity.guard';
import { canonicalRole } from '../identity/roles';
import { required } from '../common/config';

export type Provider = 'google' | 'github';
export interface ExternalIdentity { subject:string; email:string; name:string; verified:boolean }
export const digest = (value:string) => createHash('sha256').update(value).digest('hex');
const opaque = () => randomBytes(32).toString('base64url');
export function validOpaque(value:unknown): value is string { return typeof value==='string' && /^[A-Za-z0-9_-]{43}$/.test(value); }
export function oauthOrigin():string {
  const url = new URL(required('OAUTH_PUBLIC_ORIGIN'));
  if (url.username || url.password || url.search || url.hash || url.pathname!=='/') throw new Error('Invalid OAuth origin');
  if (process.env.OAUTH_ENV==='production') {
    if(url.origin!=='https://vigiarural.site') throw new Error('Production OAuth requires the public domain');
  } else if(process.env.OAUTH_ENV!=='local') throw new Error('OAUTH_ENV must be local or production');
  if(url.protocol!=='https:' && !(url.protocol==='http:' && ['localhost','127.0.0.1','[::1]'].includes(url.hostname))) throw new Error('OAuth requires HTTPS outside loopback');
  return url.origin;
}

/** Server-side provider tokens never leave Auth; clients receive a one-use, browser-bound ticket. */
@Injectable()
export class OAuthService {
  constructor(private readonly db:Database,private readonly tokens:Tokens,private readonly outbox:Outbox) {}
  async start(provider:Provider) {
    const origin=oauthOrigin(), clientId=required(provider.toUpperCase()+'_CLIENT_ID');
    required(provider.toUpperCase()+'_CLIENT_SECRET');
    const state=opaque(),browser=opaque(),verifier=opaque(),nonce=opaque();
    await this.db.transaction(async client=>{
      await client.query('DELETE FROM auth_oauth_attempts WHERE expires_at < now()');
      await client.query('DELETE FROM auth_oauth_tickets WHERE expires_at < now()');
      await client.query("INSERT INTO auth_oauth_attempts(state_hash,browser_hash,provider,verifier,nonce,expires_at) VALUES($1,$2,$3,$4,$5,now()+interval '10 minutes')",[digest(state),digest(browser),provider,verifier,nonce]);
    });
    const url=new URL(provider==='google'?'https://accounts.google.com/o/oauth2/v2/auth':'https://github.com/login/oauth/authorize');
    url.search=new URLSearchParams({client_id:clientId,redirect_uri:origin+'/api/auth/callback/'+provider,response_type:'code',scope:provider==='google'?'openid email profile':'read:user user:email',state,code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256',...(provider==='google'?{nonce}:{})}).toString();
    return {url:url.toString(),browser};
  }
  async callback(provider:Provider,code:unknown,state:unknown,browser:unknown) {
    const origin=oauthOrigin();
    if(!validOpaque(state)||!validOpaque(browser)||typeof code!=='string'||!code||code.length>4096) throw new UnauthorizedException('Invalid OAuth callback');
    // DELETE commits before contacting the provider: callback replay fails across all replicas.
    const attempt=await this.db.transaction(async client=>(await client.query('DELETE FROM auth_oauth_attempts WHERE state_hash=$1 AND browser_hash=$2 AND provider=$3 AND expires_at>now() RETURNING verifier,nonce',[digest(state),digest(browser),provider])).rows[0]);
    if(!attempt) throw new UnauthorizedException('Invalid or expired OAuth state');
    const external=await this.providerIdentity(provider,code,attempt.verifier,attempt.nonce,origin);
    const ticket=await this.finish(provider,external,browser);
    return origin+'/auth/callback#code='+ticket;
  }
  private async json(url:string,init:RequestInit={}) {
    try {
      const response=await fetch(url,{...init,redirect:'error',signal:AbortSignal.timeout(8000)});
      if(!response.ok) throw new Error();
      return await response.json();
    } catch { throw new UnauthorizedException('External authentication failed'); }
  }
  async providerIdentity(provider:Provider,code:string,verifier:string,nonce:string,origin:string):Promise<ExternalIdentity> {
    const clientId=required(provider.toUpperCase()+'_CLIENT_ID');
    const data=await this.json(provider==='google'?'https://oauth2.googleapis.com/token':'https://github.com/login/oauth/access_token',{
      method:'POST',headers:{Accept:'application/json','Content-Type':'application/x-www-form-urlencoded'},
      body:new URLSearchParams({code,code_verifier:verifier,client_id:clientId,client_secret:required(provider.toUpperCase()+'_CLIENT_SECRET'),redirect_uri:origin+'/api/auth/callback/'+provider,grant_type:'authorization_code'})});
    if(provider==='google') {
      try {
        const ticket=await new OAuth2Client(clientId).verifyIdToken({idToken:data.id_token,audience:clientId});
        const payload=ticket.getPayload();
        if(!payload || (payload as unknown as {nonce:string}).nonce!==nonce || payload.email_verified!==true || !payload.sub || !payload.email) throw new Error();
        return {subject:payload.sub,email:payload.email,name:payload.name??payload.email,verified:true};
      } catch { throw new UnauthorizedException('Invalid Google identity'); }
    }
    if(typeof data.access_token!=='string') throw new UnauthorizedException('Invalid GitHub token');
    const headers={Authorization:'Bearer '+data.access_token,Accept:'application/vnd.github+json','User-Agent':'vigia-rural-auth'};
    const user=await this.json('https://api.github.com/user',{headers});
    // Always obtain verified email metadata; the public /user email is not proof of verification.
    const emails=await this.json('https://api.github.com/user/emails',{headers});
    const email=Array.isArray(emails)?emails.find(e=>e.verified===true && e.primary===true)??emails.find(e=>e.verified===true):null;
    if(!Number.isSafeInteger(user.id)||user.id<=0||!email?.email) throw new UnauthorizedException('GitHub requires verified email');
    return {subject:String(user.id),email:email.email,name:user.name||user.login,verified:true};
  }
  async finish(provider:Provider,external:ExternalIdentity,browser:string):Promise<string> {
    if(!external.verified || !external.subject || external.subject.length>255 || typeof external.email!=='string' || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(external.email) || external.email.length>254) throw new UnauthorizedException('Verified identity required');
    const email=external.email.toLowerCase().trim(),ticket=opaque();
    await this.db.transaction(async client=>{
      // Serialize linking with account administration and concurrent callbacks; preserve existing IDs/hashes.
      await client.query('SELECT pg_advisory_xact_lock(731005)');
      let row=(await client.query('SELECT u.* FROM auth_users u JOIN auth_external_accounts e ON u.id=e.user_id WHERE e.provider=$1 AND e.subject=$2 FOR UPDATE OF u',[provider,external.subject])).rows[0];
      if(!row) {
        row=(await client.query('SELECT * FROM auth_users WHERE lower(username)=$1 FOR UPDATE',[email])).rows[0];
        if(!row) row=(await client.query("INSERT INTO auth_users(id,name,username,password_hash,role,created_at) VALUES($1,$2,$3,NULL,'ConsultationUser',now()) RETURNING *",[randomUUID(),String(external.name||email).trim().slice(0,150)||email,email])).rows[0];
        if(!row.is_active) throw new UnauthorizedException();
        await client.query('INSERT INTO auth_external_accounts(provider,subject,user_id,verified_email) VALUES($1,$2,$3,$4)',[provider,external.subject,row.id,email]);
      }
      if(!row.is_active) throw new UnauthorizedException();
      await client.query("INSERT INTO auth_oauth_tickets(ticket_hash,browser_hash,user_id,provider,expires_at) VALUES($1,$2,$3,$4,now()+interval '1 minute')",[digest(ticket),digest(browser),row.id,provider]);
    });
    return ticket;
  }
  async exchange(code:unknown,browser:unknown) {
    if(!validOpaque(code)||!validOpaque(browser)) throw new UnauthorizedException();
    return this.db.transaction(async client=>{
      const ticket=(await client.query('DELETE FROM auth_oauth_tickets WHERE ticket_hash=$1 AND browser_hash=$2 AND expires_at>now() RETURNING user_id,provider',[digest(code),digest(browser)])).rows[0];
      if(!ticket) throw new UnauthorizedException();
      const row=(await client.query('UPDATE auth_users SET last_access_at=now() WHERE id=$1 AND is_active=true RETURNING *',[ticket.user_id])).rows[0];
      if(!row) throw new UnauthorizedException();
      const user=identityRow(row);
      await this.outbox.enqueue(client,user,'Login',user,'Inicio de sesión externo: '+ticket.provider+'.');
      return {...this.tokens.issue(user),name:user.name,email:user.username,role:canonicalRole(user.role),provider:ticket.provider};
    });
  }
}
