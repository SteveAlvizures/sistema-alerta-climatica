import { Injectable, OnApplicationBootstrap } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

import { Role } from '../roles/entities/role.entity.js';
import { Permission } from '../permissions/entities/permission.entity.js';

@Injectable()
export class DatabaseSeedService implements OnApplicationBootstrap {
  constructor(
    @InjectRepository(Role)
    private readonly rolesRepository: Repository<Role>,

    @InjectRepository(Permission)
    private readonly permissionsRepository: Repository<Permission>,
  ) {}

  async onApplicationBootstrap(): Promise<void> {
    await this.seedPermissions();
    await this.seedRoles();
  }

  private async seedPermissions(): Promise<void> {
    const permissions = [
      ['users.read', 'Consultar usuarios'],
      ['users.write', 'Crear y modificar usuarios'],
      ['communities.read', 'Consultar comunidades'],
      ['communities.write', 'Administrar comunidades'],
      ['sensors.read', 'Consultar sensores'],
      ['sensors.write', 'Administrar sensores'],
      ['alerts.read', 'Consultar alertas'],
      ['alerts.write', 'Administrar alertas'],
      ['reports.read', 'Consultar reportes'],
      ['reports.generate', 'Generar reportes'],
    ];

    for (const [name, description] of permissions) {
      const existing = await this.permissionsRepository.findOne({
        where: { name },
      });

      if (!existing) {
        await this.permissionsRepository.save(
          this.permissionsRepository.create({
            name,
            description,
          }),
        );
      }
    }
  }

  private async seedRoles(): Promise<void> {
    const definitions: Record<string, string[]> = {
      Administrator: [
        'users.read',
        'users.write',
        'communities.read',
        'communities.write',
        'sensors.read',
        'sensors.write',
        'alerts.read',
        'alerts.write',
        'reports.read',
        'reports.generate',
      ],

      Operator: [
        'communities.read',
        'communities.write',
        'sensors.read',
        'sensors.write',
        'alerts.read',
        'alerts.write',
        'reports.read',
        'reports.generate',
      ],

      ConsultationUser: [
        'communities.read',
        'sensors.read',
        'alerts.read',
        'reports.read',
      ],
    };

    const descriptions: Record<string, string> = {
      Administrator: 'Administrador del sistema',
      Operator: 'Operador del sistema',
      ConsultationUser: 'Usuario de consulta',
    };

    for (const [roleName, permissionNames] of Object.entries(
      definitions,
    )) {
      let role = await this.rolesRepository.findOne({
        where: { name: roleName },
        relations: {
          permissions: true,
        },
      });

      if (!role) {
        role = this.rolesRepository.create({
          name: roleName,
          description: descriptions[roleName],
          permissions: [],
        });
      }

      const permissions = await this.permissionsRepository
        .createQueryBuilder('permission')
        .where('permission.name IN (:...names)', {
          names: permissionNames,
        })
        .getMany();

      role.permissions = permissions;

      await this.rolesRepository.save(role);
    }
  }
}
