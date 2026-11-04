import { IsIn, IsString } from 'class-validator';

export class ChangeUserRoleDto {
  @IsString()
  @IsIn(['ADMIN', 'OPERATOR', 'VIEWER'])
  role: string;
}
