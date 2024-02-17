import { forwardRef, Module } from "@nestjs/common";
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { CommonModule } from '../common/common.module';
import { ApiConfigService } from '../common/config.service';
import { NotificationModule } from '../notification/notification.module';
import { UserModule } from '../user/user.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { LocalStrategy } from './strategies';
import { JwtStrategy } from './strategies/jwt.strategy';
import { PublicStrategy } from "./strategies/public.strategy";


@Module({
  imports: [
    forwardRef(() => UserModule),
    PassportModule.register({ defaultStrategy: 'jwt' }),
    JwtModule.registerAsync({
      imports: [CommonModule],
      inject: [ApiConfigService],
      useFactory: (configService: ApiConfigService) => {
        return {
          global: true,
          privateKey: configService.authConfig.accessTokenPrivateKey,
          publicKey: configService.authConfig.accessTokenPublicKey,
          signOptions: {
            algorithm: 'RS256',
            expiresIn: configService.authConfig.accessTokenExpiresIn,
          },
        };
      },
    }),
    // PassportModule.register({ defaultStrategy: 'jwt-access-token' }),
    NotificationModule,
  ],
  controllers: [AuthController],
  providers: [AuthService, JwtStrategy, PublicStrategy], // LocalStrategy, JwtAccessTokenStrategy],
  exports: [AuthService, JwtModule],
})
export class AuthModule {}
