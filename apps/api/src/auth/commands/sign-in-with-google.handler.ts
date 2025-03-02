import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { AuthService } from '@/auth/services/auth.service';
import { ValidateSignInWithGoogleCommand } from '@/auth/commands/validate-sign-in-with-google.command';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

@CommandHandler(ValidateSignInWithGoogleCommand)
export class SignInWithGoogleHandler implements ICommandHandler<ValidateSignInWithGoogleCommand> {
  private readonly logger = new Logger(SignInWithGoogleHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly publisher: EventPublisher,
  ) {}

  async execute(command: ValidateSignInWithGoogleCommand): Promise<SignInResponseDto> {
    // const { token } = command;
    //
    // return await this.authService.signInWithGoogle(token);
    throw new Error('Method not implemented.');
  }
}
