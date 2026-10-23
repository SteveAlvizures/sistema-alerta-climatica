import { Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import * as bcrypt from 'bcrypt';
import { randomUUID } from 'crypto';

import { UsersService } from '../users/users.service.js';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  name: string;
  email: string;
  role: string;
}

@Injectable()
export class AuthService {
  constructor(
    private readonly usersService: UsersService,
    private readonly jwtService: JwtService,
    private readonly configService: ConfigService,
  ) {}

  async login(request: LoginRequest): Promise<LoginResponse> {
    const username =
      request.username?.trim().toLowerCase() ?? '';

    const password = request.password ?? '';

    if (!username || !password) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    let user =
      await this.usersService.findByUsername(username);

    if (!user) {
      const matches =
        await this.usersService.findByName(username);

      user =
        matches.length === 1
          ? matches[0]
          : null;
    }

    if (!user || !user.isActive) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    const passwordValid = await bcrypt.compare(
      password,
      user.passwordHash,
    );

    if (!passwordValid) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    const permissions =
      user.role.permissions?.map(
        (permission) => permission.name,
      ) ?? [];

    const expiresIn =
      this.configService.get<string>(
        'JWT_EXPIRES_IN',
      ) ?? '1h';

    const issuer =
      this.configService.getOrThrow<string>(
        'JWT_ISSUER',
      );

    const audience =
      this.configService.getOrThrow<string>(
        'JWT_AUDIENCE',
      );

    const accessToken =
      await this.jwtService.signAsync(
        {
          sub: user.id,
          email: user.email,
          name: user.name,
          role: user.role.name,
          permissions,
          jti: randomUUID(),
        },
        {
          issuer,
          audience,
          expiresIn: expiresIn as any,
        },
      );

    const decoded =
      this.jwtService.decode(accessToken);

    const expiresAt =
      typeof decoded === 'object' &&
      decoded?.exp
        ? new Date(
            decoded.exp * 1000,
          ).toISOString()
        : new Date().toISOString();

    await this.usersService.update(
      user.id,
      {
        lastAccessAt: new Date(),
      },
    );

    return {
      accessToken,
      expiresAt,
      name: user.name,
      email: user.email,
      role: user.role.name,
    };
  }
}
