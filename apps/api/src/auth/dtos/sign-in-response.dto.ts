import { ApiProperty, ApiSchema } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ValidateNested } from 'class-validator';

import { UserDto } from '@/user/dtos/user.dto';

@ApiSchema({ name: 'SignInResponse' })
export class SignInResponseDto {
  @ApiProperty()
  accessToken: string;
  @ApiProperty()
  refreshToken: string;
  @ApiProperty({ type: UserDto })
  @ValidateNested()
  @Type(() => UserDto)
  user: UserDto;

  protected constructor(partial: Partial<SignInResponseDto>) {
    Object.assign(this, partial);
  }

  public static create(partial: Partial<SignInResponseDto>): SignInResponseDto {
    return new SignInResponseDto(partial);
  }
}
