import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { UserEntity } from '../../user/entities';

export class LocalSignInResponseDto {
  @ApiProperty()
  accessToken: string;
  @ApiProperty()
  refreshToken: string;
  @ApiProperty({ type: UserEntity })
  @Type(() => {
    return UserEntity;
  })
  user: UserEntity;

  constructor(partial: Partial<LocalSignInResponseDto>) {
    Object.assign(this, partial);
  }
}
