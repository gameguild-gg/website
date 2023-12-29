import { Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { Strategy } from 'passport-local';
import { AuthService } from '../auth.service';
import { LocalSignInDto } from '../dtos/local-sign-in.dto';

@Injectable()
export class LocalStrategy extends PassportStrategy(Strategy) {
  constructor(private readonly service: AuthService) {
    super({
      usernameField: 'email',
      passwordField: 'password',
    });
  }

  async validate(username: string, password: string) {
    try {
      return await this.service.validateLocalSignIn({
        email: username,
        password: password,
      } as LocalSignInDto);
    } catch (exception) {
      throw exception;
    }
  }
}
