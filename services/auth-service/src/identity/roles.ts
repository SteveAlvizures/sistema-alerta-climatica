export type Role = 'Administrator' | 'Operator' | 'ConsultationUser';
export type OfficialRole = 'ADMIN' | 'OPERATOR' | 'VIEWER';
export function canonicalRole(role:string):OfficialRole {
  const normalized=normalizeRole(role);
  return normalized==='Administrator'?'ADMIN':normalized==='Operator'?'OPERATOR':'VIEWER';
}
export function normalizeRole(role: string): Role {
  if(role==='ADMIN') return 'Administrator';
  if(role==='OPERATOR') return 'Operator';
  if(role==='VIEWER' || role==='Viewer') return 'ConsultationUser';
  if (role === 'User') return 'ConsultationUser';
  if (role === 'Administrator' || role === 'Operator' || role === 'ConsultationUser') return role;
  throw new Error('Invalid persisted role');
}
export function permissions(role: Role): string[] {
  return ['AuthenticatedUser','ConsultEvents', ...(role === 'Administrator' || role === 'Operator' ? ['OperateSystem'] : []),
    ...(role === 'Administrator' ? ['AdministratorOnly'] : [])];
}
