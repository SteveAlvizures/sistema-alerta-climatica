import { BadRequestException, Injectable, UnauthorizedException } from '@nestjs/common';
import { Database } from '../persistence/database';
import { Tokens } from './tokens';
import { Identity, identityRow } from '../identity/identity.guard';
import { CredentialVerifier } from '../identity/credential-verifier';
import { Outbox } from '../integration/outbox';

@Injectable()
export class AuthService {
  constructor(private readonly db:Database,private readonly tokens:Tokens,private readonly outbox:Outbox,private readonly credentials:CredentialVerifier) {}
  async login(body:Record<string,unknown>) {
    if(typeof body?.username!=='string'||typeof body.password!=='string')
      throw new BadRequestException('El usuario y la contraseña son obligatorios.');
    const username=body.username.trim().toLowerCase(),password=body.password;
    if(!username||!password.trim()||username.length>254||password.length>4096) throw new UnauthorizedException('Usuario o contraseña incorrectos.');
    return this.db.transaction(async client=>{
      let rows=await client.query('SELECT * FROM auth_users WHERE lower(username)=$1 AND is_active=true FOR UPDATE',[username]);
      if(!rows.rowCount) rows=await client.query('SELECT * FROM auth_users WHERE lower(name)=$1 AND is_active=true LIMIT 2 FOR UPDATE',[username]);
      if(rows.rowCount!==1 || !await this.credentials.verify(rows.rows[0].password_hash,password)) throw new UnauthorizedException('Usuario o contraseña incorrectos.');
      const saved=await client.query('UPDATE auth_users SET last_access_at=now() WHERE id=$1 RETURNING *',[rows.rows[0].id]);
      const user=identityRow(saved.rows[0]);
      await this.outbox.enqueue(client,user,'Login',user,'Inicio de sesión exitoso.');
      return {...this.tokens.issue(user),name:user.name,email:user.username,role:user.role};
    });
  }
  async logout(identity:Identity):Promise<void> {
    await this.db.transaction(client=>this.outbox.enqueue(client,identity,'Logout',identity,'Cierre de sesión del cliente.'));
  }
}
