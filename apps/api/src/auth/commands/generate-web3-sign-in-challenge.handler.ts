import { Logger } from '@nestjs/common';
import { CommandBus, CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';

import { GenerateWeb3SignInChallengeCommand } from '@/auth/commands/generate-web3-sign-in-challenge.command';

@CommandHandler(GenerateWeb3SignInChallengeCommand)
export class GenerateWeb3SignInChallengeHandler implements ICommandHandler<GenerateWeb3SignInChallengeCommand> {
  private readonly logger = new Logger(GenerateWeb3SignInChallengeHandler.name);

  constructor(
    private readonly commandBus: CommandBus,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: GenerateWeb3SignInChallengeCommand): Promise<string> {
    return '';
  }
}
