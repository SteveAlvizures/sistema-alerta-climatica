import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { PoolClient } from 'pg';
import { Database } from '../persistence/database';
import { IdentityPasswords } from '../auth/passwords';
import { Identity, identityRow } from '../identity/identity.guard';
import { Role } from '../identity/roles';
import { Outbox } from '../integration/outbox';

export function validText(value:unknown,max:number,label:string):string {
  if (typeof value!=='string' || !value.trim() || value.trim().length>max) throw new BadRequestException(label);
  return value.trim();
}
export function validUsername(value:unknown):string {
  const result=validText(value,254,'El login es obligatorio, sin espacios y admite hasta 254 caracteres.').toLowerCase();
  if (/[\s\p{Cc}]/u.test(result)) throw new BadRequestException('El login es obligatorio, sin espacios y admite hasta 254 caracteres.');
  return result;
}
export function validRole(value:unknown):Role {
  if (value!=='Administrator' && value!=='Operator' && value!=='ConsultationUser') throw new BadRequestException('Selecciona un rol válido: Administrator, Operator o ConsultationUser.');
  return value;
}
export function response(user:Identity) {
  const {permissions:_,...result}=user; return result;
}
@Injectable()
export class UsersService {
  constructor(private readonly db:Database,private readonly outbox:Outbox) {}
  private async find(client:PoolClient,id:string):Promise<Identity> {
    const result=await client.query('SELECT * FROM auth_users WHERE id=$1',[id]);
    if (!result.rowCount) throw new NotFoundException('No se encontró el usuario.');
    return identityRow(result.rows[0]);
  }
  async get(id:string) { return this.db.transaction(async client=>response(await this.find(client,id))); }
  async list(query:Record<string,unknown>) {
    const page=query.page===undefined?1:Number(query.page),pageSize=query.pageSize===undefined?20:Number(query.pageSize);
    if (!Number.isSafeInteger(page)||page<1||page>2147483647||!Number.isInteger(pageSize)||pageSize<1||pageSize>100) throw new BadRequestException('La página debe ser positiva y el tamaño estar entre 1 y 100.');
    const clauses:string[]=[],values:unknown[]=[];
    const bind=(value:unknown)=>{values.push(value);return '$'+values.length;};
    if (query.search!==undefined && typeof query.search!=='string') throw new BadRequestException('Búsqueda inválida.');
    if (typeof query.search==='string' && query.search.trim()) {
      const text=query.search.trim().toLowerCase(); if(text.length>254) throw new BadRequestException('La búsqueda no puede superar 254 caracteres.');
      const index=bind(text); clauses.push(`(strpos(lower(name),${index})>0 OR strpos(lower(username),${index})>0)`);
    }
    if (query.role!==undefined && query.role!=='') {
      const role=validRole(query.role); clauses.push(role==='ConsultationUser'?"role IN ('ConsultationUser','User')":`role=${bind(role)}`);
    }
    if (query.isActive!==undefined) {
      if (query.isActive!=='true' && query.isActive!=='false') throw new BadRequestException('Estado inválido.');
      clauses.push(`is_active=${bind(query.isActive==='true')}`);
    }
    const where=clauses.length?' WHERE '+clauses.join(' AND '):'';
    // Same snapshot for totals and items; no user rows/hashes leave this boundary.
    return this.db.transaction(async client=>{
      await client.query('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ');
      const count=Number((await client.query('SELECT count(*) FROM auth_users'+where,values)).rows[0].count);
      const rows=await client.query('SELECT * FROM auth_users'+where+` ORDER BY name,id LIMIT ${bind(pageSize)} OFFSET ${bind(Math.min((page-1)*pageSize,2147483647))}`,values);
      const pages=Math.ceil(count/pageSize);
      return {data:rows.rows.map(row=>response(identityRow(row))),pageIndex:page,pageSize,totalPages:pages,totalCount:count,hasPrevious:page>1,hasNext:page<pages};
    });
  }
  async create(body:Record<string,unknown>,actor:Identity) {
    const name=validText(body.name,150,'El nombre es obligatorio y admite hasta 150 caracteres.');
    const username=validUsername(body.username),role=validRole(body.role);
    if(typeof body.password!=='string'||!body.password.trim()||body.password.length<8||body.password.length>128) throw new BadRequestException('La contraseña debe tener entre 8 y 128 caracteres y no estar vacía.');
    const hash=await new IdentityPasswords().hash(body.password);
    return this.db.transaction(async client=>{
      const rows=await client.query('INSERT INTO auth_users(id,name,username,password_hash,role,created_at) VALUES($1,$2,$3,$4,$5,now()) RETURNING *',[randomUUID(),name,username,hash,role]);
      const target=identityRow(rows.rows[0]); await this.outbox.enqueue(client,actor,'UsuarioCreado',target,'Se creó una cuenta de usuario.');return response(target);
    });
  }
  async update(id:string,body:Record<string,unknown>,actor:Identity) {
    const name=validText(body.name,150,'El nombre es obligatorio y admite hasta 150 caracteres.'),username=validUsername(body.username);
    return this.db.transaction(async client=>{
      await this.find(client,id);
      const rows=await client.query('UPDATE auth_users SET name=$2,username=$3 WHERE id=$1 RETURNING *',[id,name,username]);
      const target=identityRow(rows.rows[0]); await this.outbox.enqueue(client,actor,'UsuarioEditado',target,'Se actualizó el nombre o login de la cuenta.');return response(target);
    });
  }
  async change(id:string,body:Record<string,unknown>,actor:Identity,kind:'role'|'status') {
    const value=kind==='role'?validRole(body.role):body.isActive;
    if(kind==='status'&&typeof value!=='boolean') throw new BadRequestException('El estado activo es obligatorio.');
    return this.db.transaction(async client=>{
      // Serialize administrator demotions/deactivations to protect the last active admin.
      await client.query('SELECT pg_advisory_xact_lock(731005)');
      const target=await this.find(client,id);
      const storedRole=kind==='role'?(await client.query('SELECT role FROM auth_users WHERE id=$1',[id])).rows[0].role:target.role;
      if(id===actor.id && (kind==='status'&&!value || kind==='role'&&value!=='Administrator')) throw new ConflictException(kind==='status'?'No puedes desactivar tu propia cuenta administrativa.':'No puedes quitar el rol Administrator de tu propia cuenta.');
      if(target.isActive&&target.role==='Administrator'&&(kind==='status'&&!value||kind==='role'&&value!=='Administrator')) {
        const others=await client.query("SELECT 1 FROM auth_users WHERE id<>$1 AND is_active=true AND role='Administrator' LIMIT 1",[id]);
        if(!others.rowCount) throw new ConflictException('Debe permanecer al menos un administrador activo.');
      }
      if(kind==='role'?storedRole===value:target.isActive===value) return response(target);
      const rows=await client.query(`UPDATE auth_users SET ${kind==='role'?'role':'is_active'}=$2 WHERE id=$1 RETURNING *`,[id,value]);
      const updated=identityRow(rows.rows[0]);
      const action=kind==='role'?'RolUsuarioCambiado':value?'UsuarioActivado':'UsuarioDesactivado';
      const description=kind==='role'?`Se asignó el rol ${value}.`:value?'Se activó la cuenta.':'Se desactivó la cuenta.';
      await this.outbox.enqueue(client,actor,action,updated,description); return response(updated);
    });
  }
}
