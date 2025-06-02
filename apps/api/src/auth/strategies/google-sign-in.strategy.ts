import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { Profile, Strategy, VerifyCallback } from 'passport-google-oauth20';

import { GOOGLE_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';
import { ValidateGoogleSignInCommand } from '@/auth/commands/validate-google-sign-in.command';
import { googleOauthConfig } from '@/auth/config/google-oauth.config';
import { GoogleSignInPayloadDto } from '@/auth/dtos/google-sign-in-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

@Injectable()
export class GoogleSignInStrategy extends PassportStrategy(Strategy, GOOGLE_SIGN_IN_STRATEGY_KEY) {
  private readonly logger = new Logger(GoogleSignInStrategy.name);

  constructor(
    @Inject(googleOauthConfig.KEY)
    readonly config: ConfigType<typeof googleOauthConfig>,
    private readonly commandBus: CommandBus,
  ) {
    super(config);
  }

  public async validate(accessToken: string, refreshToken: string, profile: Profile, done: VerifyCallback): Promise<UserDto> {
    const payload: GoogleSignInPayloadDto = { accessToken, refreshToken, profile };
    try {
      const user: UserDto = await this.commandBus.execute(ValidateGoogleSignInCommand.create(payload));
      done(null, user);
      return user;
    } catch (error) {
      done(error, null);
      throw error;
    }
  }
}
