import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString } from 'class-validator';

export class EthereumSigninChallengeResponseDto {
  @ApiProperty()
  @IsString({
    message: 'error.invalidMessage: Message must be a string.',
  })
  @IsNotEmpty({
    message: 'error.emptyMessage: Message must not be empty.',
  })
  message: string;
}
