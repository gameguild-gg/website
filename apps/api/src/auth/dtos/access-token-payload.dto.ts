import { ApiProperty, ApiSchema } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsString, MaxLength } from 'class-validator';
import { SUB_MAX_LENGTH } from '@/auth/auth.constants';
import { TokenType } from '@/auth/dtos/token-type.enum';

@ApiSchema({ name: 'AccessTokenPayload' })
export class AccessTokenPayloadDto {
  @ApiProperty()
  @IsString({ message: 'error.invalidSub: Sub must be a string.' })
  @IsNotEmpty({ message: 'error.invalidSub: Sub must not be empty.' })
  @MaxLength(SUB_MAX_LENGTH, { message: `error.invalidSub: Sub must be shorter than or equal to ${SUB_MAX_LENGTH} characters.` })
  sub: string;

  @ApiProperty({ enum: TokenType })
  @IsEnum(TokenType, { message: 'error.invalidTokenType: Token type is invalid.' })
  type: TokenType;
}
