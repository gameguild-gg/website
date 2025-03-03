import { Logger } from '@nestjs/common';
import { CommandBus, CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';

import { GenerateAccessTokenCommand } from '@/auth/commands/generate-access-token.command';
import { GenerateRefreshTokenCommand } from '@/auth/commands/generate-refresh-token.command';
import { GenerateSignInResponseCommand } from '@/auth/commands/generate-sign-in-response.command';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

@CommandHandler(GenerateSignInResponseCommand)
export class GenerateSignInResponseHandler implements ICommandHandler<GenerateSignInResponseCommand> {
  private readonly logger = new Logger(GenerateSignInResponseHandler.name);

  constructor(
    private readonly commandBus: CommandBus,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: GenerateSignInResponseCommand): Promise<SignInResponseDto> {
    const { user } = command;

    const [accessToken, refreshToken] = await Promise.all([
      this.commandBus.execute(GenerateAccessTokenCommand.create(user)),
      this.commandBus.execute(GenerateRefreshTokenCommand.create(user)),
    ]);

    return SignInResponseDto.create({ accessToken, refreshToken, user });
  }
}
