import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsString, IsStrongPassword } from 'class-validator';

export class LocalSignInRequest {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsEmail({}, {})
  readonly username?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsEmail({}, {})
  readonly email!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsStrongPassword({}, {})
  readonly password!: string;
}
