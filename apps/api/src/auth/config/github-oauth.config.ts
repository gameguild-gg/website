import { registerAs } from '@nestjs/config';
import { StrategyOptions } from 'passport-github2';

import { GITHUB_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';
import { environment } from '@/config/environment.config';

export const gitHubOauthConfig = registerAs(
  GITHUB_SIGN_IN_STRATEGY_KEY,
  (): StrategyOptions => ({
    clientID: environment.getString('GITHUB_CLIENT_ID'),
    clientSecret: environment.getString('GITHUB_CLIENT_SECRET'),
    callbackURL: environment.getString('GITHUB_CALLBACK_URL'),
    scope: ['email', 'profile'],
  }),
);
