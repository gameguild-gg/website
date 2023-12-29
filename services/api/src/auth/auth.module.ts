import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { CommonModule } from "../common/common.module";
import { ApiConfigService } from "../common/config.service";
import { NotificationModule } from '../notification/notification.module';
import { UserModule } from '../user/user.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { JwtAccessTokenStrategy, LocalStrategy } from './strategies';

@Module({
  imports: [
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
      }
    }),
    PassportModule.register({ defaultStrategy: 'jwt-access-token' }),
    NotificationModule,
    UserModule, // forwardRef(() => UserModule),
  ],
  controllers: [AuthController],
  providers: [AuthService, LocalStrategy, JwtAccessTokenStrategy],
  exports: [AuthService],
})
export class AuthModule {}
