import {
  Column,
  Entity,
  ManyToMany,
  PrimaryGeneratedColumn,
} from 'typeorm';

import { Role } from '../../roles/entities/role.entity.js';

@Entity('permissions')
export class Permission {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ unique: true, length: 120 })
  name: string;

  @Column({ nullable: true, length: 250 })
  description?: string;

  @ManyToMany(() => Role, (role) => role.permissions)
  roles: Role[];
}
