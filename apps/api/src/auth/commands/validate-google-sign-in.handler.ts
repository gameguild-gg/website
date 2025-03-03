import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { AuthService } from '@/auth/services/auth.service';
import { ValidateGoogleSignInCommand } from '@/auth/commands/validate-google-sign-in.command';
import { UserDto } from '@/user/dtos/user.dto';

@CommandHandler(ValidateGoogleSignInCommand)
export class ValidateGoogleSignInHandler implements ICommandHandler<ValidateGoogleSignInCommand> {
  private readonly logger = new Logger(ValidateGoogleSignInHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: ValidateGoogleSignInCommand): Promise<UserDto> {
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
