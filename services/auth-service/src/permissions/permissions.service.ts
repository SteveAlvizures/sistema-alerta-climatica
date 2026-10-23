import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

import { Permission } from './entities/permission.entity.js';

@Injectable()
export class PermissionsService {
  constructor(
    @InjectRepository(Permission)
    private readonly permissionsRepository: Repository<Permission>,
  ) {}

  findAll(): Promise<Permission[]> {
    return this.permissionsRepository.find();
  }

  findById(id: string): Promise<Permission | null> {
    return this.permissionsRepository.findOne({
      where: { id },
    });
  }

  findByName(name: string): Promise<Permission | null> {
    return this.permissionsRepository.findOne({
      where: { name },
    });
  }

  create(permissionData: Partial<Permission>): Promise<Permission> {
    const permission = this.permissionsRepository.create(permissionData);
    return this.permissionsRepository.save(permission);
  }
}
