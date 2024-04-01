import { Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
// import { Strategy, VerifyCallback } from 'passport-google-oauth20';
import { AuthService } from '../auth.service';

@Injectable()
export class GoogleStrategy extends PassportStrategy(Strategy, 'google') {
  constructor(private readonly service: AuthService) {
    super({
      usernameField: 'email',
      passwordField: 'password',
    });
  }
}
