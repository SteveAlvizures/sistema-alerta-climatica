export type Role = 'Administrator' | 'Operator' | 'ConsultationUser';
export function normalizeRole(role: string): Role {
  if (role === 'User') return 'ConsultationUser';
  if (role === 'Administrator' || role === 'Operator' || role === 'ConsultationUser') return role;
  throw new Error('Invalid persisted role');
}
export function permissions(role: Role): string[] {
  return ['AuthenticatedUser','ConsultEvents', ...(role === 'Administrator' || role === 'Operator' ? ['OperateSystem'] : []),
    ...(role === 'Administrator' ? ['AdministratorOnly'] : [])];
}
