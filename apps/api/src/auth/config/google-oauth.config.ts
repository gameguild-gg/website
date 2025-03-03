import { registerAs } from '@nestjs/config';
import { StrategyOptions } from 'passport-google-oauth20';
import { environment } from '@/config/environment.config';
import { GOOGLE_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';

export const googleOauthConfig = registerAs(
  GOOGLE_SIGN_IN_STRATEGY_KEY,
  (): StrategyOptions => ({
    clientID: environment.getString('GOOGLE_CLIENT_ID'),
    clientSecret: environment.getString('GOOGLE_CLIENT_SECRET'),
    callbackURL: environment.getString('GOOGLE_CALLBACK_URL'),
    scope: ['email', 'profile'],
  }),
);
