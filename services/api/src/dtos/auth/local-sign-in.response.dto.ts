import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { UserEntity } from '../../user/entities';
import { ValidateNested } from 'class-validator';

export class LocalSignInResponseDto {
  @ApiProperty()
  accessToken: string;
  @ApiProperty()
  refreshToken: string;
  @ApiProperty({ type: UserEntity })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  constructor(partial: Partial<LocalSignInResponseDto>) {
    Object.assign(this, partial);
  }
}
