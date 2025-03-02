import { Logger } from '@nestjs/common';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { LocalSignInCommand } from '@/auth/commands/local-sign-in.command';

@CommandHandler(LocalSignInCommand)
export class LocalSignInHandler implements ICommandHandler<LocalSignInCommand> {
  private readonly logger = new Logger(LocalSignInHandler.name);

  constructor(private readonly publisher: EventPublisher) {}

  async execute(command: LocalSignInCommand) {}
}
