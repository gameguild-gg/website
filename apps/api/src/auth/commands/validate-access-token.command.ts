import { Command } from '@nestjs/cqrs';

import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateAccessTokenCommand extends Command<UserDto> {
  protected constructor(public readonly data: LocalSignInRequestDto) {
    super();
  }

  public static create(data: LocalSignInRequestDto): ValidateAccessTokenCommand {
    return new ValidateAccessTokenCommand(data);
  }
}
