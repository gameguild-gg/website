import { Command } from '@nestjs/cqrs';

import { UserDto } from '@/user/dtos/user.dto';

export class GenerateWeb3SignInChallengeCommand extends Command<string> {
  protected constructor(public readonly user: UserDto) {
    super();
  }

  public static create(user: UserDto): GenerateWeb3SignInChallengeCommand {
    return new GenerateWeb3SignInChallengeCommand(user);
  }
}
