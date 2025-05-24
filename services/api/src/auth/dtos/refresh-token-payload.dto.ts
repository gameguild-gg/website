import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsString, MaxLength } from 'class-validator';
import { TokenType } from './token-type.enum';
import { IsUsername } from '../../common/decorators/validator.decorator';

export class RefreshTokenPayloadDto {
  @ApiProperty()
  @IsString({ message: 'error.invalidSub: Sub must be a string.' })
  @IsNotEmpty({ message: 'error.invalidSub: Sub must not be empty.' })
  @MaxLength(256, {
    message: 'error.invalidSub: Sub must be shorter than or equal to 256 characters.',
  })
  sub: string;

  @ApiProperty()
  @IsUsername()
  username: string;

  // token type
  @ApiProperty({ enum: TokenType })
  @IsEnum(TokenType, {
    message: 'error.invalidTokenType: Token type is invalid.',
  })
  type: TokenType;
}
