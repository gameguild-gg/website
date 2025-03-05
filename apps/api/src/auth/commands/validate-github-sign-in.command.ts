import { Command } from '@nestjs/cqrs';

import { GitHubSignInPayloadDto } from '@/auth/dtos/github-sign-in-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateGitHubSignInCommand extends Command<UserDto> {
  protected constructor(public readonly data: GitHubSignInPayloadDto) {
    super();
  }

  public static create(data: GitHubSignInPayloadDto): ValidateGitHubSignInCommand {
    return new ValidateGitHubSignInCommand(data);
  }
}
