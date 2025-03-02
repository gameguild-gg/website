import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { refreshTokenConfig } from '@/auth/config/refresh-token.config';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { AuthController } from '@/auth/controllers/auth.controller';
import { AuthService } from '@/auth/services/auth.service';
import { LocalStrategy } from '@/auth/strategies/local.strategy';
import { AccessTokenStrategy } from '@/auth/strategies/access-token.strategy';
import { RefreshTokenStrategy } from '@/auth/strategies/refresh-token.strategy';

@Module({
  imports: [
    ConfigModule.forFeature(accessTokenConfig),
    ConfigModule.forFeature(refreshTokenConfig),
    JwtModule.registerAsync(accessTokenConfig.asProvider()),
    PassportModule.register({ defaultStrategy: 'access-token' }),
    // forwardRef(() => UserModule), // TODO: Implement UserModule
  ],
  controllers: [AuthController],
  providers: [
    AuthService,
    LocalStrategy,
    AccessTokenStrategy,
    RefreshTokenStrategy,
    //
  ],
  exports: [AuthService],
})
export class AuthModule {}
