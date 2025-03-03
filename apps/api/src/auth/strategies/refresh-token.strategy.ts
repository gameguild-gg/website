import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';

import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { refreshTokenConfig as RefreshTokenConfig } from '@/auth/config/refresh-token.config';

@Injectable()
export class RefreshTokenStrategy extends PassportStrategy(Strategy, REFRESH_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(RefreshTokenStrategy.name);

  constructor(
    @Inject(RefreshTokenConfig.KEY)
    private readonly refreshTokenConfig: ConfigType<typeof RefreshTokenConfig>,
    private readonly commandBus: CommandBus,
  ) {
    super({
      // TODO: get this from a configService or the authService.
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      algorithms: ['HS512'],
      secretOrKey: 'secret',
    });
  }

  async validate() {}
}
