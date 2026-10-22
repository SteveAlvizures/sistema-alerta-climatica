import 'reflect-metadata';
import { readFile } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { Database } from './database';
import { normalizeRole } from '../identity/roles';
import { validText, validUsername } from '../users/users.service';
interface LegacyUser { id:string;name:string;email:string;passwordHash:string;role:string;isActive:boolean;createdAt:string;lastAccessAt:string|null }
export async function importUsers(db:Database,items:LegacyUser[]):Promise<{inserted:number;unchanged:number}> {
  if(!Array.isArray(items)) throw new Error('Invalid import document');
  return db.transaction(async client=>{
    await client.query('SELECT pg_advisory_xact_lock(731005)');
    let inserted=0,unchanged=0;
    for(const user of items) {
      if(!user||!/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(user.id)
        ||typeof user.passwordHash!=='string'||user.passwordHash.length>500||!user.passwordHash
        ||typeof user.isActive!=='boolean'||!Number.isFinite(Date.parse(user.createdAt))
        ||(user.lastAccessAt!==null&&!Number.isFinite(Date.parse(user.lastAccessAt)))) throw new Error('Invalid import record');
      validText(user.name,150,'Invalid name');validUsername(user.email);normalizeRole(user.role);
      const existing=await client.query('SELECT imported_source FROM auth_users WHERE id=$1',[user.id]);
      if(existing.rowCount) {
        // Never overwrite accounts already managed by Auth. A different source is an error.
        const source=existing.rows[0].imported_source;
        if(!source||Object.keys(user).some(key=>source[key]!==user[key as keyof LegacyUser])) throw new Error('Import conflict: existing ID has different source data');
        unchanged++;continue;
      }
      await client.query('INSERT INTO auth_users(id,name,username,password_hash,role,is_active,created_at,last_access_at,imported_source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)',
        [user.id,user.name,user.email,user.passwordHash,user.role,user.isActive,user.createdAt,user.lastAccessAt,JSON.stringify(user)]);
      inserted++;
    }
    return {inserted,unchanged};
  });
}
if(require.main===module) {
  (async()=>{
    const db=new Database();
    try {
      await db.onModuleInit();
      const file=process.argv[2];if(!file) throw new Error('Expected export file path');
      const data=JSON.parse(file==='-'?readFileSync(0,'utf8'):await readFile(file,'utf8'));
      console.info(JSON.stringify(await importUsers(db,data)));
    } finally {await db.onModuleDestroy();}
  })().catch(()=>{console.error('User import failed. No partial import was committed; review source privately.');process.exitCode=1;});
}
