import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { refreshTokenConfig } from '@/auth/config/refresh-token.config';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { googleOauthConfig } from '@/auth/config/google-oauth.config';
import { AuthController } from '@/auth/controllers/auth.controller';
import { AuthService } from '@/auth/services/auth.service';
import { LocalSignInStrategy } from '@/auth/strategies/local-sign-in.strategy';
import { GoogleSignInStrategy } from '@/auth/strategies/google-sign-in.strategy';
import { AccessTokenStrategy } from '@/auth/strategies/access-token.strategy';
import { RefreshTokenStrategy } from '@/auth/strategies/refresh-token.strategy';
import { ValidateGoogleSignInHandler } from '@/auth/commands/validate-google-sign-in.handler';

@Module({
  imports: [
    ConfigModule.forFeature(accessTokenConfig),
    ConfigModule.forFeature(refreshTokenConfig),
    ConfigModule.forFeature(googleOauthConfig),
    JwtModule.registerAsync(accessTokenConfig.asProvider()),
    PassportModule.register({ defaultStrategy: 'access-token' }),
  ],
  controllers: [AuthController],
  providers: [
    AuthService,
    LocalSignInStrategy,
    GoogleSignInStrategy,
    AccessTokenStrategy,
    RefreshTokenStrategy,
    //
    ValidateGoogleSignInHandler,
  ],
  exports: [AuthService],
})
export class AuthModule {}
