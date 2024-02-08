import { ApiProperty } from "@nestjs/swagger";
import { IsEmail, IsNotEmpty, IsString, MaxLength } from "class-validator";

export class AccessTokenPayloadDto {
  @ApiProperty()
  @IsString({message: 'error.invalidSub: Sub must be a string.'})
  @IsNotEmpty({message: 'error.invalidSub: Sub must not be empty.'})
  @MaxLength(256, {message: 'error.invalidSub: Sub must be shorter than or equal to 256 characters.'})
  sub: string;
  
  @ApiProperty()
  @IsEmail({}, {message: 'error.invalidEmail: It must be a valid email address.'})
  email: string;
}
