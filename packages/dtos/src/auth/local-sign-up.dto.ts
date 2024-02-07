import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsEmail,
  IsLowercase,
  IsNotEmpty,
  IsString,
  IsStrongPassword,
  MaxLength,
} from 'class-validator';

export class LocalSignUpDto {
  // TODO: Add a username field.
  @ApiProperty()
  @IsString({ message: 'error.invalidUsername: Username must be a string.' })
  @IsNotEmpty({ message: 'error.invalidUsername: Username must not be empty.' })
  @MaxLength(32, {
    message:
      'error.invalidUsername: Username must be shorter than or equal to 32 characters.',
  })
  @IsAlphanumeric('en-US', {
    message:
      'error.invalidUsername: Username must be alphanumeric without any special characters.',
  })
  readonly username: string;

  @ApiProperty()
  @IsString({ message: 'error.invalidEmail: Email must be a string.' })
  @IsNotEmpty({ message: 'error.invalidEmail: Email must not be empty.' })
  @IsLowercase({ message: 'error.invalidEmail: Email must be lowercase.' })
  @IsEmail(
    {},
    {
      message: 'error.invalidEmail: It must be a valid email address.',
    },
  )
  readonly email: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  // TODO: Add a max length for the password.
  // @MaxLength()
  @IsStrongPassword(
    {
      minLength: 8,
      minLowercase: 1,
      minUppercase: 1,
      minNumbers: 1,
      minSymbols: 1,
    },
    {
      message:
        'error.weakPassword: Password is too weak. It must have at least 8 characters, including 1 lowercase, 1 uppercase, 1 number, and 1 symbol.',
    },
  )
  readonly password: string;
}
