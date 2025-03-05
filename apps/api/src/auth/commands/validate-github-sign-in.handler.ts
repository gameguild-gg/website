import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';

import { ValidateGitHubSignInCommand } from '@/auth/commands/validate-github-sign-in.command';
import { AuthService } from '@/auth/services/auth.service';
import { UserDto } from '@/user/dtos/user.dto';

@CommandHandler(ValidateGitHubSignInCommand)
export class ValidateGitHubSignInHandler implements ICommandHandler<ValidateGitHubSignInCommand> {
  private readonly logger = new Logger(ValidateGitHubSignInHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: ValidateGitHubSignInCommand): Promise<UserDto> {
    const { profile } = command.data;

    const providerData: Partial<UserDto> = {
      googleId: profile.id,
      email: profile.emails?.[0].value,
    };

    const user = await this.authService.signInWithProvider(providerData);

    await this.authService.validateSignIn(user);

    return user;
  }
}
