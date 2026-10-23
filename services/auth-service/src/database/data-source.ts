import 'reflect-metadata';
import { config } from 'dotenv';
import { DataSource } from 'typeorm';

import { User } from '../users/entities/user.entity.js';
import { Role } from '../roles/entities/role.entity.js';
import { Permission } from '../permissions/entities/permission.entity.js';

config();

const AppDataSource = new DataSource({
  type: 'postgres',

  host: process.env.AUTH_DB_HOST,
  port: Number(process.env.AUTH_DB_PORT ?? 5432),

  database: process.env.AUTH_DB_NAME,
  username: process.env.AUTH_DB_USER,
  password: process.env.AUTH_DB_PASSWORD,

  entities: [
    User,
    Role,
    Permission,
  ],

  migrations: [
    'dist/database/migrations/*.js',
  ],

  migrationsTableName: 'migrations',

  synchronize: false,
});

export default AppDataSource;
