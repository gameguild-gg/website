import { ApiProperty } from '@nestjs/swagger';
import {
  IsEthereumAddress,
  IsInt,
  IsNotEmpty,
  IsNumberString,
  IsString,
} from 'class-validator';

export class EthereumSigninChallengeRequestDto {
  @ApiProperty()
  @IsString({
    message: 'error.invalidSignature: Signature must be a string.',
  })
  domain: string;

  @ApiProperty()
  @IsNotEmpty({
    message: 'error.emptyEthereumAddress: Ethereum address must not be empty.',
  })
  @IsString({
    message: 'error.invalidEthereumAddress: Ethereum address must be a string.',
  })
  @IsEthereumAddress({
    message: 'error.invalidEthereumAddress: Ethereum address is invalid.',
  })
  address: string;

  @ApiProperty()
  @IsString({
    message: 'error.invalidSignature: Signature must be a string.',
  })
  uri: string;

  @ApiProperty()
  @IsString({
    message: 'error.invalidSignature: Signature must be a string.',
  })
  version: string;

  @ApiProperty()
  @IsNumberString(
    { no_symbols: true },
    {
      message: 'error.invalidChainId: Chain ID must be a number.',
    },
  )
  chainId: string;

  @ApiProperty()
  @IsString({
    message: 'error.invalidSignature: Signature must be a string.',
  })
  nonce: string;
}