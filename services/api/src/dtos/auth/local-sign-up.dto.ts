import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsPassword, IsUsername } from '../../common/decorators/validator.decorator';

export class LocalSignUpDto {
  @ApiProperty()
  @IsUsername()
  readonly username: string;

  @ApiProperty()
  @IsEmail()
  readonly email: string;

  @ApiProperty()
  @IsPassword()
  readonly password: string;
}
