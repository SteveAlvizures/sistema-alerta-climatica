import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { Permission } from './entities/permission.entity.js';
import { PermissionsService } from './permissions.service.js';

@Module({
  imports: [TypeOrmModule.forFeature([Permission])],
  providers: [PermissionsService],
  exports: [PermissionsService],
})
export class PermissionsModule {}
