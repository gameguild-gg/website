import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';

import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { ValidateRefreshTokenCommand } from '@/auth/commands/validate-refresh-token.command';
import { refreshTokenConfig as RefreshTokenConfig } from '@/auth/config/refresh-token.config';
import { RefreshTokenPayloadDto } from '@/auth/dtos/refresh-token-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

@Injectable()
export class RefreshTokenStrategy extends PassportStrategy(Strategy, REFRESH_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(RefreshTokenStrategy.name);

  constructor(
    @Inject(RefreshTokenConfig.KEY)
    private readonly config: ConfigType<typeof RefreshTokenConfig>,
    private readonly commandBus: CommandBus,
  ) {
    super({
      // TODO: get this from a configService or the authService.
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: config.verifyOptions.ignoreExpiration,
      algorithms: [...config.verifyOptions.algorithms],
      secretOrKey: 'secret',
    });
  }

  public async validate(payload: RefreshTokenPayloadDto): Promise<UserDto> {
    return this.commandBus.execute(ValidateRefreshTokenCommand.create(payload));
  }
}
