import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { ValidateLocalSignInCommand } from '@/auth/commands/validate-local-sign-in.command';
import { AuthService } from '@/auth/services/auth.service';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

@CommandHandler(ValidateLocalSignInCommand)
export class ValidateLocalSignInHandler implements ICommandHandler<ValidateLocalSignInCommand> {
  private readonly logger = new Logger(ValidateLocalSignInHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: ValidateLocalSignInCommand): Promise<SignInResponseDto> {
    const { data } = command;

    // return await this.authService.signInWithEmailAndPassword(data);
    throw new Error('Method not implemented.');
  }
}
