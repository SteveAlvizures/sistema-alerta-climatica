import { CanActivate, ExecutionContext, Injectable, UnauthorizedException } from '@nestjs/common';
import { timingSafeEqual } from 'node:crypto';
import { Request } from 'express';
import { required } from '../common/config';
@Injectable()
export class InternalGuard implements CanActivate {
  private readonly key=Buffer.from(required('AUTH_INTERNAL_KEY',32));
  canActivate(context:ExecutionContext):boolean {
    const supplied=context.switchToHttp().getRequest<Request>().headers['x-internal-service-key'];
    const bytes=typeof supplied==='string' ? Buffer.from(supplied) : Buffer.alloc(0);
    if (bytes.length!==this.key.length || !timingSafeEqual(bytes,this.key)) throw new UnauthorizedException();
    return true;
  }
}
