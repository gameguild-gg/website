import { Command } from '@nestjs/cqrs';

import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateWeb3SignInCommand extends Command<UserDto> {
  protected constructor(public readonly data: LocalSignInRequestDto) {
    super();
  }

  public static create(data: LocalSignInRequestDto): ValidateWeb3SignInCommand {
    return new ValidateWeb3SignInCommand(data);
  }
}
