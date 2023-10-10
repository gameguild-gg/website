import { Injectable } from "@nestjs/common";
import { PassportStrategy } from "@nestjs/passport";
import { ExtractJwt, Strategy } from 'passport-jwt';
import { AccessTokenPayloadDto } from "../dtos/access-token-payload.dto";

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
  constructor() {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      // TODO: get this from config service.
      algorithms: ['HS512'],
      secretOrKey: 'secret',
      // secretOrKey: process.env.PUBLIC_KEY,
    })
  }

  async validate(payload: AccessTokenPayloadDto) {
    return { id: payload.sub, email: payload.email };
  }
}