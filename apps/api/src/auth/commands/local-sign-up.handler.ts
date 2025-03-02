import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { LocalSignUpCommand } from '@/auth/commands/local-sign-up.command';
import { AuthService } from '@/auth/services/auth.service';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

@CommandHandler(LocalSignUpCommand)
export class LocalSignUpHandler implements ICommandHandler<LocalSignUpCommand> {
  private readonly logger = new Logger(LocalSignUpHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: LocalSignUpCommand): Promise<SignInResponseDto> {
    // const { data } = command;
    // return await this.authService.signUpWithEmailAndPassword(data);
    throw new Error('Method not implemented.');
  }
}
