import { Injectable, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { PoolClient } from 'pg';
import { Database } from '../persistence/database';
import { required } from '../common/config';
import { Identity } from '../identity/identity.guard';

/** Durable HTTP delivery with retries; receivers must deduplicate by the stable event ID. */
@Injectable()
export class Outbox implements OnModuleInit, OnModuleDestroy {
  private timer?:NodeJS.Timeout;
  private running=false;
  private pending?:Promise<void>;
  constructor(private readonly db:Database) {}
  async enqueue(client:PoolClient, actor:Identity, action:string, target:Identity, description:string):Promise<void> {
    // The caller's transaction commits the identity change and its audit event together.
    const id=randomUUID();
    await client.query('INSERT INTO auth_outbox(id,payload) VALUES($1,$2)',[id,JSON.stringify({
      id,actor,target,action,description,occurredAt:new Date().toISOString(),affectedRecordId:target.id,
    })]);
  }
  onModuleInit():void {
    required('AUTH_INTERNAL_KEY',32); required('AUTH_AUDIT_URL');
    this.timer=setInterval(() => { this.pending=this.deliver(); },1000); this.timer.unref();
  }
  async deliver():Promise<void> {
    if (this.running) return;
    this.running=true;
    try {
      // Transaction/advisory lock ensures ordered delivery with multiple service replicas.
      await this.db.transaction(async client => {
        const locked=await client.query('SELECT pg_try_advisory_xact_lock(731004) AS locked');
        if (!locked.rows[0].locked) return;
        const rows=await client.query('SELECT id,payload FROM auth_outbox WHERE delivered_at IS NULL ORDER BY occurred_at,id LIMIT 25');
        for (const row of rows.rows) {
          await client.query('UPDATE auth_outbox SET attempts=attempts+1 WHERE id=$1',[row.id]);
          try {
            const response=await fetch(required('AUTH_AUDIT_URL'),{method:'POST',headers:{
              'Content-Type':'application/json','X-Internal-Service-Key':required('AUTH_INTERNAL_KEY',32),
              'X-Correlation-ID':row.id,
            },body:JSON.stringify(row.payload),signal:AbortSignal.timeout(3000)});
            // A lost acknowledgement can cause redelivery even if the receiver already stored the event.
            if (!response.ok) break;
            await client.query('UPDATE auth_outbox SET delivered_at=now() WHERE id=$1',[row.id]);
          } catch { break; }
        }
      });
    } catch { /* Keep durable events pending. No secrets/exceptions written to logs. */ }
    finally { this.running=false; }
  }
  async onModuleDestroy():Promise<void> { if (this.timer) clearInterval(this.timer); await this.pending; }
}
