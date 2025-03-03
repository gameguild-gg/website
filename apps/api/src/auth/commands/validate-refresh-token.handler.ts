import { Logger, UnauthorizedException } from '@nestjs/common';
import { CommandHandler, ICommandHandler, QueryBus } from '@nestjs/cqrs';

import { ValidateRefreshTokenCommand } from '@/auth/commands/validate-refresh-token.command';
import { AuthService } from '@/auth/services/auth.service';
import { validateHash } from '@/auth/utils';
import { UserDto } from '@/user/dtos/user.dto';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';

@CommandHandler(ValidateRefreshTokenCommand)
export class ValidateRefreshTokenHandler implements ICommandHandler<ValidateRefreshTokenCommand> {
  private readonly logger = new Logger(ValidateRefreshTokenHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly queryBus: QueryBus,
  ) {}

  public async execute(command: ValidateRefreshTokenCommand): Promise<UserDto> {
    const { email, password } = command.data;

    const user = await this.queryBus.execute(new FindOneUserQuery({ where: { email } }));

    await this.authService.validateSignIn(user);

    const { passwordHash, passwordSalt } = user;

    if (!validateHash(password, passwordHash, passwordSalt)) throw new UnauthorizedException();

    return user;
  }
}
