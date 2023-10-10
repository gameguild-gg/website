import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsString, IsStrongPassword, } from 'class-validator';

export class LocalSignUpDto {
  // TODO: Add a username field.
  // @ApiProperty()
  // @IsString()
  // // TODO: Add a max length for the username.
  // // @MaxLength()
  // @IsAlphanumeric(
  //   'en-US',
  //   {
  //     message: 'error.invalidUsername: Username must be alphanumeric without any special characters.'
  //   }
  // )
  // readonly username: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsEmail(
    {},
    {
      message: 'error.invalidEmail: It must be a valid email address.'
    }
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
      minSymbols: 1
    },
    {
      message: 'error.weakPassword: Password is too weak. It must have at least 8 characters, including 1 lowercase, 1 uppercase, 1 number, and 1 symbol.'
    }
  )
  readonly password: string;
}
