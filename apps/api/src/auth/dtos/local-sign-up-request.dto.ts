import { ApiProperty, ApiSchema } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsString, IsStrongPassword } from 'class-validator';

@ApiSchema({ name: 'LocalSignUpRequest' })
export class LocalSignUpRequestDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsEmail({}, {})
  public readonly username?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsEmail({}, {})
  public readonly email!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsStrongPassword({}, {})
  public readonly password!: string;
}
