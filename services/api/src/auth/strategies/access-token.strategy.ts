import {Injectable, Logger} from '@nestjs/common';
import {PassportStrategy} from '@nestjs/passport';
import {ExtractJwt, Strategy} from 'passport-jwt';
import {ApiConfigService} from '../../common/config.service';
import {AccessTokenPayloadDto} from "../../dtos/auth";
import {UserService} from "../../user/user.service";

@Injectable()
export class AccessTokenStrategy extends PassportStrategy(Strategy, "access-token") {
  private readonly logger = new Logger(AccessTokenStrategy.name);
  constructor(private readonly configService: ApiConfigService, private readonly userService: UserService) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      // TODO: get this from config service.
      algorithms: ['RS256'],

      secretOrKey: configService.authConfig.accessTokenPublicKey,
    });
  }

  async validate(payload: AccessTokenPayloadDto) {
    // TODO: Should perform a better validation here.
    return await this.userService.findOne({where: {id: payload.sub}});

  }
}
