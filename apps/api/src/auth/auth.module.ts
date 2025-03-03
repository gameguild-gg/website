import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { ACCESS_TOKEN_STRATEGY_KEY, REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { refreshTokenConfig } from '@/auth/config/refresh-token.config';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { googleOauthConfig } from '@/auth/config/google-oauth.config';
import { AuthController } from '@/auth/controllers/auth.controller';
import { GenerateEmailVerificationTokenHandler } from '@/auth/commands/generate-email-verification-token.handler';
import { GenerateSignInResponseHandler } from '@/auth/commands/generate-sign-in-response.handler';
import { ValidateGoogleSignInHandler } from '@/auth/commands/validate-google-sign-in.handler';
import { ValidateLocalSignInHandler } from '@/auth/commands/validate-local-sign-in.handler';
import { LocalSignInStrategy } from '@/auth/strategies/local-sign-in.strategy';
import { GoogleSignInStrategy } from '@/auth/strategies/google-sign-in.strategy';
import { AccessTokenStrategy } from '@/auth/strategies/access-token.strategy';
import { RefreshTokenStrategy } from '@/auth/strategies/refresh-token.strategy';
import { AuthService } from '@/auth/services/auth.service';

@Module({
  imports: [
    ConfigModule.forFeature(accessTokenConfig),
    ConfigModule.forFeature(refreshTokenConfig),
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
    AccessTokenStrategy,
    RefreshTokenStrategy,
    //
    // GenerateAccessTokenHandler,
    // GenerateRefreshTokenHandler,
    //
    GenerateEmailVerificationTokenHandler,
    //
    GenerateSignInResponseHandler,
    //
    ValidateGoogleSignInHandler,
    ValidateLocalSignInHandler,
    //
  ],
  exports: [AuthService],
})
export class AuthModule {}
