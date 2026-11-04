import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  Unique,
} from 'typeorm';

import { User } from '../../users/entities/user.entity.js';

export type ExternalProvider =
  | 'google'
  | 'github';

@Entity('external_accounts')
@Unique(
  'UQ_external_provider_user',
  [
    'provider',
    'providerUserId',
  ],
)
@Unique(
  'UQ_external_user_provider',
  [
    'user',
    'provider',
  ],
)
export class ExternalAccount {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @ManyToOne(
    () => User,
    {
      nullable: false,
      onDelete: 'CASCADE',
    },
  )
  @JoinColumn({
    name: 'user_id',
  })
  user: User;

  @Column({
    type: 'varchar',
    length: 50,
  })
  provider: ExternalProvider;

  @Column({
    name: 'provider_user_id',
    type: 'varchar',
    length: 255,
  })
  providerUserId: string;

  @Column({
    name: 'provider_email',
    type: 'varchar',
    length: 254,
    nullable: true,
  })
  providerEmail: string | null;

  @CreateDateColumn({
    name: 'created_at',
    type: 'timestamptz',
  })
  createdAt: Date;
}
