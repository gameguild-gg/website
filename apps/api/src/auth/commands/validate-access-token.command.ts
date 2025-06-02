import { Command } from '@nestjs/cqrs';

import { AccessTokenPayloadDto } from '@/auth/dtos/access-token-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateAccessTokenCommand extends Command<UserDto> {
  protected constructor(public readonly data: AccessTokenPayloadDto) {
    super();
  }

  public static create(data: AccessTokenPayloadDto): ValidateAccessTokenCommand {
    return new ValidateAccessTokenCommand(data);
  }
}
