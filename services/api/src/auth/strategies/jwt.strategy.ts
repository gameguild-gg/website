import { Injectable, UnauthorizedException } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { ApiConfigService } from '../../common/config.service';
import { UserService } from '../../user/user.service';
import { UserEntity } from '../../user/entities';
import { TokenType } from '../dtos/token-type.enum';
import { AccessTokenPayloadDto } from '../dtos/access-token-payload.dto';
import { RefreshTokenPayloadDto } from '../dtos/refresh-token-payload.dto';

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy, 'access-token') {
  constructor(
    configService: ApiConfigService,
    private userService: UserService,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      secretOrKey: configService.authConfig.accessTokenPublicKey,
      algorithms: ['RS256'],
    });
  }

  async validate(args: AccessTokenPayloadDto): Promise<UserEntity> {
    if (args.type !== TokenType.AccessToken) {
      throw new UnauthorizedException(); // todo: do we need this line?
    }

    const user = await this.userService.findOne({
      where: { id: args.sub },
    });

    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    return user;
  }
}

@Injectable()
export class RefreshTokenStrategy extends PassportStrategy(
  Strategy,
  'refresh-token',
) {
  constructor(
    configService: ApiConfigService,
    private userService: UserService,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      secretOrKey: configService.authConfig.refreshTokenPublicKey,
      algorithms: ['RS256'],
    });
  }

  async validate(args: RefreshTokenPayloadDto): Promise<UserEntity> {
    // todo: add extra security layers, check if the user is active, etc
    if (args.type !== TokenType.RefreshToken) {
      throw new UnauthorizedException();
    }

    const user = await this.userService.findOne({
      where: { id: args.sub },
    });

    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    return user;
  }
}
