import { ApiProperty } from '@nestjs/swagger';
import { IsEthereumAddress, IsInt, IsNotEmpty, IsNumberString, IsString } from 'class-validator';

export class EthereumSigninChallengeRequestDto {
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
}
