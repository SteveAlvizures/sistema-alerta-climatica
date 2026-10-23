import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { TypeOrmModule } from '@nestjs/typeorm';

import { AppController } from './app.controller.js';
import { AppService } from './app.service.js';

import { AuthModule } from './auth/auth.module.js';
import { UsersModule } from './users/users.module.js';
import { RolesModule } from './roles/roles.module.js';
import { PermissionsModule } from './permissions/permissions.module.js';
import { DatabaseModule } from './database/database.module.js';
import { HealthModule } from './health/health.module.js';

import { User } from './users/entities/user.entity.js';
import { Role } from './roles/entities/role.entity.js';
import { Permission } from './permissions/entities/permission.entity.js';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: '.env',
    }),

    TypeOrmModule.forRootAsync({
      imports: [
        ConfigModule,
      ],

      inject: [
        ConfigService,
      ],

      useFactory: (
        config: ConfigService,
      ) => ({
        type: 'postgres',

        host:
          config.getOrThrow<string>(
            'AUTH_DB_HOST',
          ),

        port: Number(
          config.getOrThrow<string>(
            'AUTH_DB_PORT',
          ),
        ),

        database:
          config.getOrThrow<string>(
            'AUTH_DB_NAME',
          ),

        username:
          config.getOrThrow<string>(
            'AUTH_DB_USER',
          ),

        password:
          config.getOrThrow<string>(
            'AUTH_DB_PASSWORD',
          ),

        entities: [
          User,
          Role,
          Permission,
        ],

        synchronize: false,

        retryAttempts: 5,
        retryDelay: 3000,
      }),
    }),

    DatabaseModule,
    AuthModule,
    UsersModule,
    RolesModule,
    PermissionsModule,
    HealthModule,
  ],

  controllers: [
    AppController,
  ],

  providers: [
    AppService,
  ],
})
export class AppModule {}
