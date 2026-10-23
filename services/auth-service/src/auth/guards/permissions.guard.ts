import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import { Reflector } from '@nestjs/core';

import { PERMISSIONS_KEY } from '../decorators/permissions.decorator.js';

interface AuthenticatedUser {
  id: string;
  email: string;
  name: string;
  role: string;
  permissions: string[];
  jti: string;
}

@Injectable()
export class PermissionsGuard implements CanActivate {
  constructor(private readonly reflector: Reflector) {}

  canActivate(context: ExecutionContext): boolean {
    const requiredPermissions =
      this.reflector.getAllAndOverride<string[]>(
        PERMISSIONS_KEY,
        [context.getHandler(), context.getClass()],
      );

    if (!requiredPermissions || requiredPermissions.length === 0) {
      return true;
    }

    const request = context.switchToHttp().getRequest<{
      user?: AuthenticatedUser;
    }>();

    const user = request.user;

    if (!user) {
      throw new ForbiddenException(
        'No fue posible identificar al usuario.',
      );
    }

    const userPermissions = user.permissions ?? [];

    const allowed = requiredPermissions.every(
      (permission) =>
        userPermissions.includes(permission),
    );

    if (!allowed) {
      throw new ForbiddenException(
        'No tienes permisos para realizar esta acción.',
      );
    }

    return true;
  }
}
