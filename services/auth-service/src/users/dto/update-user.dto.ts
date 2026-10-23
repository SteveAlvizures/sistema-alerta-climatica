import {
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';

export class UpdateUserDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(150)
  name: string;

  @IsString()
  @IsNotEmpty()
  @MaxLength(254)
  username: string;
}
