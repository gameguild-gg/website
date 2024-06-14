import { ApiProperty } from "@nestjs/swagger";
import { IsEthereumAddress, IsNotEmpty, IsString } from "class-validator";

export class EthereumChallengeResponseDto {
  @ApiProperty()
  @IsString({message: 'error.invalidAccountAddress: Account address must be a string.'})
  @IsNotEmpty({message: 'error.invalidAccountAddress: Account address must not be empty.'})
  @IsEthereumAddress({message: 'error.invalidAccountAddress: Account address must be a valid Ethereum address.'})
  accountAddress: string;

  @ApiProperty()
  @IsString({message: 'error.invalidSignature: Signature must be a string.'})
  @IsNotEmpty({message: 'error.invalidSignature: Signature must not be empty.'})
  // todo: add validation to be a hex
  signature: string;

  @ApiProperty()
  @IsString({message: 'error.invalidMessage: Message must be a string.'})
  @IsNotEmpty({message: 'error.invalidMessage: Message must not be empty.'})
  message: string;
}