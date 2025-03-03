import { Logger, UnauthorizedException } from '@nestjs/common';
import { CommandHandler, ICommandHandler, QueryBus } from '@nestjs/cqrs';

import { ValidateWeb3SignInCommand } from '@/auth/commands/validate-web3-sign-in.command';
import { AuthService } from '@/auth/services/auth.service';
import { validateHash } from '@/auth/utils';
import { UserDto } from '@/user/dtos/user.dto';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';

@CommandHandler(ValidateWeb3SignInCommand)
export class ValidateWeb3SignInHandler implements ICommandHandler<ValidateWeb3SignInCommand> {
  private readonly logger = new Logger(ValidateWeb3SignInHandler.name);

  constructor(
    private readonly authService: AuthService,
    private readonly queryBus: QueryBus,
  ) {}

  public async execute(command: ValidateWeb3SignInCommand): Promise<UserDto> {
    const { email, password } = command.data;

    const user = await this.queryBus.execute(new FindOneUserQuery({ where: { email } }));

    await this.authService.validateSignIn(user);

    const { passwordHash, passwordSalt } = user;

    if (!validateHash(password, passwordHash, passwordSalt)) throw new UnauthorizedException();

    return user;
  }
}
