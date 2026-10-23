import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
} from 'typeorm';

import { Role } from '../../roles/entities/role.entity.js';

@Entity('users')
export class User {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ length: 150 })
  name: string;

  @Column({ unique: true, length: 254 })
  email: string;

  @Column({ name: 'password_hash' })
  passwordHash: string;

  @ManyToOne(() => Role, (role) => role.users, {
    eager: true,
    nullable: false,
  })
  @JoinColumn({ name: 'role_id' })
  role: Role;

  @Column({ name: 'is_active', default: true })
  isActive: boolean;

  @CreateDateColumn({ name: 'created_at' })
  createdAt: Date;

  @Column({
    name: 'last_access_at',
    type: 'timestamptz',
    nullable: true,
  })
  lastAccessAt: Date | null;
}
