import { UserDto } from '../user/user.dto';
import { ApiProperty } from '@nestjs/swagger';
import { Exclude, Type } from 'class-transformer';
import { UserEntity } from '../../user/entities';

export class LocalSignInResponseDto {
  @ApiProperty()
  accessToken: string;
  @ApiProperty()
  refreshToken: string;
  @ApiProperty({ type: UserEntity })
  @Type(() => {
    return UserDto;
  })
  @Exclude()
  user: UserDto;

  constructor(partial: Partial<LocalSignInResponseDto>) {
    Object.assign(this, partial);
  }
}
