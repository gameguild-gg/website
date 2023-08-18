import { ExtractJwt, Strategy } from 'passport-jwt';
import { PassportStrategy } from '@nestjs/passport';
import { Injectable } from '@nestjs/common';
import * as fs from 'fs';
import { JwtFromUser } from '../interfaces/JWTfromUser.interface';
import { UserFromJwt } from '../interfaces/UserFromJWT.interface';

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
  constructor() {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      secretOrKeyProvider: (request, rawJwtToken, done) => {
        const publicKey = process.env.PUBLIC_KEY;
        done(null, publicKey);
      },
      algorithms: ['RS256'],
    });
  }

  async validate(payload: JwtFromUser): Promise<UserFromJwt> {
    return { userId: payload.sub, email: payload.email };
  }
}
