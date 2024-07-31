import { IsNotEmpty, IsString } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmail } from '../../common/decorators/validator.decorator';

export class EmailDto {
  @ApiProperty()
  @IsNotEmpty({ message: 'error.invalid_email: email must be not empty.' })
  @IsString({ message: 'error.invalid_email: email must be a string.' })
  @IsEmail()
  email: string;
}
