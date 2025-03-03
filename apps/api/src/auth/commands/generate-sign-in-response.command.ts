import { Command } from '@nestjs/cqrs';

import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class GenerateSignInResponseCommand extends Command<SignInResponseDto> {
  protected constructor(public readonly user: UserDto) {
    super();
  }

  public static create(user: UserDto): GenerateSignInResponseCommand {
    return new GenerateSignInResponseCommand(user);
  }
}
