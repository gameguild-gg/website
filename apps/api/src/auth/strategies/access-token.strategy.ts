import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { ACCESS_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { accessTokenConfig as AccessTokenConfig } from '@/auth/config/access-token.config';
import { AuthService } from '../services/auth.service';

@Injectable()
export class AccessTokenStrategy extends PassportStrategy(Strategy, ACCESS_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(AccessTokenStrategy.name);

  constructor(
    @Inject(AccessTokenConfig.KEY)
    private readonly accessTokenConfig: ConfigType<typeof AccessTokenConfig>,
    private readonly authService: AuthService,
  ) {
    super({
      // TODO: get this from a configService or the authService.
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      algorithms: ['HS512'],
      secretOrKey: 'secret',
    });
  }

  public async validate() {}
}
