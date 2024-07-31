import { Injectable, UnauthorizedException } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { ApiConfigService } from '../../common/config.service';
import { UserService } from '../../user/user.service';
import { UserEntity } from '../../user/entities';
import { TokenType } from '../../dtos/auth/token-type.enum';
import { AccessTokenPayloadDto } from '../../dtos/auth/access-token-payload.dto';

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
  constructor(
    configService: ApiConfigService,
    private userService: UserService,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      secretOrKey: configService.authConfig.accessTokenPublicKey,
    });
  }

  async validate(args: AccessTokenPayloadDto): Promise<UserEntity> {
    if (args.type !== TokenType.AccessToken) {
      throw new UnauthorizedException();
    }

    const user = await this.userService.findOne({
      where: { id: args.sub } as any,
    });

    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    return user;
  }
}
