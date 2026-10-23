import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PassportStrategy } from '@nestjs/passport';
import {
  ExtractJwt,
  Strategy,
} from 'passport-jwt';

export interface JwtPayload {
  sub: string;
  email: string;
  name: string;
  role: string;
  permissions: string[];
  jti: string;
}

@Injectable()
export class JwtStrategy extends PassportStrategy(
  Strategy,
) {
  constructor(config: ConfigService) {
    super({
      jwtFromRequest:
        ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      secretOrKey:
        config.getOrThrow<string>(
          'JWT_SECRET',
        ),
      issuer:
        config.getOrThrow<string>(
          'JWT_ISSUER',
        ),
      audience:
        config.getOrThrow<string>(
          'JWT_AUDIENCE',
        ),
    });
  }

  validate(payload: JwtPayload) {
    return {
      id: payload.sub,
      email: payload.email,
      name: payload.name,
      role: payload.role,
      permissions:
        payload.permissions ?? [],
      jti: payload.jti,
    };
  }
}
