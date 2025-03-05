import { Logger } from '@nestjs/common';
import { CommandHandler, ICommandHandler, QueryBus } from '@nestjs/cqrs';

import { ValidateAccessTokenCommand } from '@/auth/commands/validate-access-token.command';
import { AuthService } from '@/auth/services/auth.service';
import { UserDto } from '@/user/dtos/user.dto';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';

@CommandHandler(ValidateAccessTokenCommand)
export class ValidateAccessTokenHandler implements ICommandHandler<ValidateAccessTokenCommand> {
  private readonly logger = new Logger(ValidateAccessTokenHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly queryBus: QueryBus,
  ) {}

  public async execute(command: ValidateAccessTokenCommand): Promise<UserDto> {
    const { sub } = command.data;

    const user = await this.queryBus.execute(new FindOneUserQuery({ where: { id: sub } }));

    await this.authService.validateSignIn(user);

    return user;
  }
}
