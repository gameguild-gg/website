import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { AuthenticateCallback } from 'passport';
import { Strategy } from 'passport-github2';

import { GITHUB_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';
import { ValidateGitHubSignInCommand } from '@/auth/commands/validate-github-sign-in.command';
import { gitHubOauthConfig } from '@/auth/config/github-oauth.config';
import { GitHubSignInPayloadDto } from '@/auth/dtos/github-sign-in-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

@Injectable()
export class GitHubSignInStrategy extends PassportStrategy(Strategy, GITHUB_SIGN_IN_STRATEGY_KEY) {
  private readonly logger = new Logger(GitHubSignInStrategy.name);

  constructor(
    @Inject(gitHubOauthConfig.KEY)
    readonly config: ConfigType<typeof gitHubOauthConfig>,
    private readonly commandBus: CommandBus,
  ) {
    super(config);
  }

  public async validate(accessToken: string, refreshToken: string, profile: any, done: AuthenticateCallback): Promise<UserDto> {
    const payload: GitHubSignInPayloadDto = { accessToken, refreshToken, profile };
    try {
      const user: UserDto = await this.commandBus.execute(ValidateGitHubSignInCommand.create(payload));
      done(null, user);
      return user;
    } catch (error) {
      done(error, null);
      throw error;
    }
  }
}
