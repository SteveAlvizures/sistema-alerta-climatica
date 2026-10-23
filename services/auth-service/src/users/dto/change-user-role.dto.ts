import { IsIn, IsString } from 'class-validator';

export class ChangeUserRoleDto {
  @IsString()
  @IsIn(['Administrator', 'Operator', 'ConsultationUser'])
  role: string;
}
