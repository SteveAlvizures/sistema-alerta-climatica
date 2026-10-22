import { Injectable, UnauthorizedException } from '@nestjs/common';
import { createHash, randomUUID } from 'node:crypto';
import jwt, { JwtPayload } from 'jsonwebtoken';
import { required } from '../common/config';
export const NAME_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
export const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

@Injectable()
export class Tokens {
  private readonly key = createHash('sha256').update(required('JWT_KEY',24),'utf8').digest();
  private readonly issuer = process.env.JWT_ISSUER ?? 'climate-alert-api';
  private readonly audience = process.env.JWT_AUDIENCE ?? 'climate-alert-client';
  private readonly minutes = Number(process.env.JWT_EXPIRATION_MINUTES ?? 60);
  constructor() { if (!Number.isInteger(this.minutes) || this.minutes <= 0) throw new Error('Invalid JWT_EXPIRATION_MINUTES'); }
  issue(user: {id:string; name:string; username:string; role:string}): {accessToken:string; expiresAt:string} {
    const now = Math.floor(Date.now()/1000), exp = now + this.minutes*60;
    const accessToken = jwt.sign({ sub:user.id, email:user.username, [NAME_CLAIM]:user.name,
      [ROLE_CLAIM]:user.role, jti:randomUUID(), nbf:now, iat:now, exp }, this.key,
      { algorithm:'HS256', issuer:this.issuer, audience:this.audience });
    return {accessToken, expiresAt:new Date(exp*1000).toISOString()};
  }
  verify(token: string): JwtPayload {
    try {
      const value = jwt.verify(token,this.key,{algorithms:['HS256'],issuer:this.issuer,audience:this.audience,clockTolerance:0});
      if (typeof value === 'string' || !value.exp || !value.sub || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.sub)) throw new Error();
      return value;
    } catch { throw new UnauthorizedException(); }
  }
}
