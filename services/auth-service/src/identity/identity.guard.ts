import { CanActivate, ExecutionContext, ForbiddenException, Injectable, UnauthorizedException } from '@nestjs/common';
import { Request } from 'express';
import { Tokens } from '../auth/tokens';
import { Database } from '../persistence/database';
import { normalizeRole, permissions, Role } from './roles';
export interface Identity { id:string; name:string; username:string; role:Role; isActive:boolean; createdAt:string; lastAccessAt:string|null; permissions:string[] }
export interface AuthRequest extends Request { identity: Identity }
export function identityRow(row: Record<string, any>): Identity {
  const role = normalizeRole(row.role);
  return { id:row.id,name:row.name,username:row.username,role,isActive:row.is_active,
    createdAt:new Date(row.created_at).toISOString(),lastAccessAt:row.last_access_at ? new Date(row.last_access_at).toISOString() : null, permissions:permissions(role) };
}
@Injectable()
export class IdentityGuard implements CanActivate {
  constructor(private readonly tokens:Tokens, private readonly db:Database) {}
  async canActivate(context:ExecutionContext):Promise<boolean> {
    const request = context.switchToHttp().getRequest<AuthRequest>();
    const authorization = request.headers.authorization;
    if (!authorization || !/^Bearer /i.test(authorization)) throw new UnauthorizedException();
    const claims = this.tokens.verify(authorization.slice(7));
    // Re-read on every protected request so an old JWT cannot retain removed roles or bypass deactivation.
    const result = await this.db.pool.query('SELECT * FROM auth_users WHERE id=$1 AND is_active=true',[claims.sub]);
    if (!result.rowCount) throw new UnauthorizedException();
    request.identity = identityRow(result.rows[0]);
    return true;
  }
}
@Injectable()
export class AdministratorGuard implements CanActivate {
  canActivate(context:ExecutionContext):boolean {
    if (context.switchToHttp().getRequest<AuthRequest>().identity?.role !== 'Administrator') throw new ForbiddenException();
    return true;
  }
}
