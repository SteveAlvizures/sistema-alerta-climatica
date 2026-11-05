export type OfficialRole = 'ADMIN' | 'OPERATOR' | 'VIEWER';
export function officialRole(role:string):OfficialRole|null {
  if(role==='ADMIN'||role==='Administrator') return 'ADMIN';
  if(role==='OPERATOR'||role==='Operator') return 'OPERATOR';
  if(['VIEWER','Viewer','ConsultationUser','User'].includes(role)) return 'VIEWER';
  return null;
}
