import { Injectable, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Pool, PoolClient } from 'pg';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { required } from '../common/config';

@Injectable()
export class Database implements OnModuleInit, OnModuleDestroy {
  readonly pool = new Pool({ host: required('AUTH_DATABASE_HOST'), port: Number(process.env.AUTH_DATABASE_PORT ?? 5432),
    database: required('AUTH_DATABASE_NAME'), user: required('AUTH_DATABASE_USER'), password: required('AUTH_DATABASE_PASSWORD'),
    max: 10, connectionTimeoutMillis: 3000, statement_timeout: 10000 });
  constructor() {
    this.pool.on('error',()=>{ /* Idle connections are replaced after database restart. */ });
    this.pool.on('connect',client=>client.on('error',()=>{ /* Active queries still reject and transactions roll back. */ }));
  }
  async onModuleInit(): Promise<void> {
    // Versioned, additive schema creation only in the service's own database.
    const sql = await readFile(resolve('migrations/001_auth.sql'), 'utf8');
    const checksum = createHash('sha256').update(sql).digest('hex');
    await this.transaction(async client => {
      await client.query('SELECT pg_advisory_xact_lock(731003)');
      await client.query('CREATE TABLE IF NOT EXISTS auth_schema_migrations (version integer PRIMARY KEY, checksum text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())');
      const applied = await client.query('SELECT checksum FROM auth_schema_migrations WHERE version=1');
      if (applied.rowCount) {
        if (applied.rows[0].checksum !== checksum) throw new Error('Auth migration checksum mismatch');
      } else {
        await client.query(sql);
        await client.query('INSERT INTO auth_schema_migrations(version,checksum) VALUES(1,$1)', [checksum]);
      }
    });
  }
  async transaction<T>(action: (client: PoolClient) => Promise<T>): Promise<T> {
    const client = await this.pool.connect();
    try { await client.query('BEGIN'); const result = await action(client); await client.query('COMMIT'); return result; }
    catch (error) { await client.query('ROLLBACK'); throw error; }
    finally { client.release(); }
  }
  async onModuleDestroy(): Promise<void> { await this.pool.end(); }
}
