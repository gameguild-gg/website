import { Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { ApiConfigService } from '../../common/config.service';
import { AccessTokenPayloadDto } from '../dtos';

@Injectable()
export class JwtAccessTokenStrategy extends PassportStrategy(
  Strategy,
  'jwt-access-token',
) {
  constructor(configService: ApiConfigService) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      // TODO: get this from config service.
      algorithms: ['RS256'],
      secretOrKey: configService.authConfig.accessTokenPublicKey,
    });
  }

  async validate(payload: AccessTokenPayloadDto) {
    return { id: payload.sub, email: payload.email };
  }
}
