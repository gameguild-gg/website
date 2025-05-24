import { ApiProperty } from '@nestjs/swagger';
import { IsEthereumAddress, IsNotEmpty, IsString } from 'class-validator';

export class EthereumWalletDto {
  @ApiProperty()
  @IsNotEmpty({ message: 'error.emptyEthereumAddress: Ethereum address must not be empty.' })
  @IsString({ message: 'error.invalidEthereumAddress: Ethereum address must be a string.' })
  @IsEthereumAddress({ message: 'error.invalidEthereumAddress: Ethereum address is invalid.' })
  address: string;
}
