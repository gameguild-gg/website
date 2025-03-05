import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';

import { ACCESS_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { ValidateAccessTokenCommand } from '@/auth/commands/validate-access-token.command';
import { accessTokenConfig as AccessTokenConfig } from '@/auth/config/access-token.config';
import { AccessTokenPayloadDto } from '@/auth/dtos/access-token-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

@Injectable()
export class AccessTokenStrategy extends PassportStrategy(Strategy, ACCESS_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(AccessTokenStrategy.name);

  constructor(
    @Inject(AccessTokenConfig.KEY)
    private readonly config: ConfigType<typeof AccessTokenConfig>,
    private readonly commandBus: CommandBus,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: config.verifyOptions.ignoreExpiration,
      algorithms: [...config.verifyOptions.algorithms],
      secretOrKey: 'secret',
    });
  }

  public async validate(payload: AccessTokenPayloadDto): Promise<UserDto> {
    return this.commandBus.execute(ValidateAccessTokenCommand.create(payload));
  }
}
