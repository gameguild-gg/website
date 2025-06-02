import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';

import { ACCESS_TOKEN_STRATEGY_KEY, REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { GenerateAccessTokenHandler } from '@/auth/commands/generate-access-token.handler';
import { GenerateEmailVerificationTokenHandler } from '@/auth/commands/generate-email-verification-token.handler';
import { GenerateRefreshTokenHandler } from '@/auth/commands/generate-refresh-token.handler';
import { GenerateSignInResponseHandler } from '@/auth/commands/generate-sign-in-response.handler';
import { GenerateWeb3SignInChallengeHandler } from '@/auth/commands/generate-web3-sign-in-challenge.handler';
import { ValidateAccessTokenHandler } from '@/auth/commands/validate-access-token.handler';
import { ValidateEmailVerificationTokenHandler } from '@/auth/commands/validate-email-verification-token.handler';
import { ValidateGoogleSignInHandler } from '@/auth/commands/validate-google-sign-in.handler';
import { ValidateLocalSignInHandler } from '@/auth/commands/validate-local-sign-in.handler';
import { ValidateRefreshTokenHandler } from '@/auth/commands/validate-refresh-token.handler';
import { ValidateWeb3SignInHandler } from '@/auth/commands/validate-web3-sign-in.handler';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { emailVerificationTokenConfig } from '@/auth/config/email-verification-token.config';
import { googleOauthConfig } from '@/auth/config/google-oauth.config';
import { refreshTokenConfig } from '@/auth/config/refresh-token.config';
import { AuthController } from '@/auth/controllers/auth.controller';
import { AuthService } from '@/auth/services/auth.service';
import { AccessTokenStrategy } from '@/auth/strategies/access-token.strategy';
import { GoogleSignInStrategy } from '@/auth/strategies/google-sign-in.strategy';
import { LocalSignInStrategy } from '@/auth/strategies/local-sign-in.strategy';
import { RefreshTokenStrategy } from '@/auth/strategies/refresh-token.strategy';

@Module({
  imports: [
    ConfigModule.forFeature(accessTokenConfig),
    ConfigModule.forFeature(refreshTokenConfig),
    ConfigModule.forFeature(emailVerificationTokenConfig),
    ConfigModule.forFeature(googleOauthConfig),
    JwtModule.registerAsync(accessTokenConfig.asProvider()),
    PassportModule.register({ defaultStrategy: [ACCESS_TOKEN_STRATEGY_KEY, REFRESH_TOKEN_STRATEGY_KEY] }),
  ],
  controllers: [AuthController],
  providers: [
    AuthService,
    //
    LocalSignInStrategy,
    GoogleSignInStrategy,
    //
    AccessTokenStrategy,
    RefreshTokenStrategy,
    //
    GenerateAccessTokenHandler,
    GenerateRefreshTokenHandler,
    //
    GenerateEmailVerificationTokenHandler,
    // GeneratePasswordResetTokenHandler,
    //
    GenerateWeb3SignInChallengeHandler,
    //
    GenerateSignInResponseHandler,
    //
    ValidateAccessTokenHandler,
    ValidateRefreshTokenHandler,
    //
    ValidateEmailVerificationTokenHandler,
    // ValidatePasswordResetTokenHandler,
    //
    ValidateGoogleSignInHandler,
    ValidateLocalSignInHandler,
    ValidateWeb3SignInHandler, // TODO: Make a custom passport.js? strategy for web3 sign in.
    //
  ],
  exports: [AuthService],
})
export class AuthModule {}
