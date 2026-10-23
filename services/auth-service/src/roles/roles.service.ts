import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

import { Role } from './entities/role.entity.js';

@Injectable()
export class RolesService {
  constructor(
    @InjectRepository(Role)
    private readonly rolesRepository: Repository<Role>,
  ) {}

  findAll(): Promise<Role[]> {
    return this.rolesRepository.find();
  }

  findById(id: string): Promise<Role | null> {
    return this.rolesRepository.findOne({
      where: { id },
    });
  }

  findByName(name: string): Promise<Role | null> {
    return this.rolesRepository.findOne({
      where: { name },
    });
  }

  create(roleData: Partial<Role>): Promise<Role> {
    const role = this.rolesRepository.create(roleData);
    return this.rolesRepository.save(role);
  }
}
