import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';

import { AuthController } from './auth.controller.js';
import { AuthService } from './auth.service.js';

import { JwtStrategy } from './strategies/jwt.strategy.js';

import { RolesGuard } from './guards/roles.guard.js';
import { PermissionsGuard } from './guards/permissions.guard.js';

import { UsersModule } from '../users/users.module.js';

@Module({
  imports: [
    ConfigModule,
    UsersModule,
    PassportModule,
    JwtModule.registerAsync({
      imports: [ConfigModule],
      inject: [ConfigService],
      useFactory: (config: ConfigService) => ({
        secret: config.getOrThrow<string>('JWT_SECRET'),
      }),
    }),
  ],
  controllers: [AuthController],
  providers: [
    AuthService,
    JwtStrategy,
    RolesGuard,
    PermissionsGuard,
  ],
  exports: [
    AuthService,
    PassportModule,
    JwtModule,
    RolesGuard,
    PermissionsGuard,
  ],
})
export class AuthModule {}
