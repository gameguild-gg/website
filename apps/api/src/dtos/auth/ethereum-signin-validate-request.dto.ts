import { ApiProperty } from '@nestjs/swagger';
import { IsEthereumAddress, IsNotEmpty, IsString } from 'class-validator';

export class EthereumSigninValidateRequestDto {
  // todo: switch this to be a nonce instead of a message
  @ApiProperty()
  @IsEthereumAddress({
    message: 'error.invalidAddress: Address must be a valid Ethereum address.',
  })
  @IsNotEmpty({ message: 'error.invalidAddress: Address must not be empty.' })
  address: string; // address, message, nonce

  @ApiProperty()
  @IsString({ message: 'error.invalidSignature: Signature must be a string.' })
  @IsNotEmpty({
    message: 'error.invalidSignature: Signature must not be empty.',
  })
  // todo: add validation to be a hex
  signature: string;
}
