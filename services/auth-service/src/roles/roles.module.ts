import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { Role } from './entities/role.entity.js';
import { RolesService } from './roles.service.js';

@Module({
  imports: [TypeOrmModule.forFeature([Role])],
  providers: [RolesService],
  exports: [RolesService],
})
export class RolesModule {}
