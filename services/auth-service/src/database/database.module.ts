import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { DatabaseSeedService } from './database-seed.service.js';

import { Role } from '../roles/entities/role.entity.js';
import { Permission } from '../permissions/entities/permission.entity.js';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      Role,
      Permission,
    ]),
  ],
  providers: [
    DatabaseSeedService,
  ],
})
export class DatabaseModule {}
