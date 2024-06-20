import { ApiProperty } from '@nestjs/swagger';
import {
  IsEmail,
  IsEthereumAddress,
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';
import { TokenType } from './token-type.enum';

export class AccessTokenPayloadDto {
  @ApiProperty()
  @IsString({ message: 'error.invalidSub: Sub must be a string.' })
  @IsNotEmpty({ message: 'error.invalidSub: Sub must not be empty.' })
  @MaxLength(256, {
    message:
      'error.invalidSub: Sub must be shorter than or equal to 256 characters.',
  })
  sub: string;

  @ApiProperty()
  @IsEmail(
    {},
    { message: 'error.invalidEmail: It must be a valid email address.' },
  )
  email: string;

  @ApiProperty()
  @IsString({ message: 'error.invalidUsername: Username must be a string.' })
  @IsNotEmpty({ message: 'error.invalidUsername: Username must not be empty.' })
  @MaxLength(256, {
    message:
      'error.invalidUsername: Username must be shorter than or equal to 256 characters.',
  })
  username: string;

  @ApiProperty()
  @IsString({ message: 'error.invalidWallet: Wallet must be a string.' })
  @IsNotEmpty({ message: 'error.invalidWallet: Wallet must not be empty.' })
  @IsEthereumAddress({
    message: 'error.invalidWallet: Wallet must be a valid Ethereum address.',
  })
  wallet: string;

  // token type
  @ApiProperty({ enum: TokenType })
  type: TokenType;
}
