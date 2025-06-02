import { Command } from '@nestjs/cqrs';

import { RefreshTokenPayloadDto } from '@/auth/dtos/refresh-token-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateRefreshTokenCommand extends Command<UserDto> {
  protected constructor(public readonly data: RefreshTokenPayloadDto) {
    super();
  }

  public static create(data: RefreshTokenPayloadDto): ValidateRefreshTokenCommand {
    return new ValidateRefreshTokenCommand(data);
  }
}
