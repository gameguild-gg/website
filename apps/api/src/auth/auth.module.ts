import { forwardRef, Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { CommonModule } from '../common/common.module';
import { ApiConfigService } from '../common/config.service';
import { NotificationModule } from '../notification/notification.module';
import { UserModule } from '../user/user.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import {
  JwtStrategy,
  PublicStrategy,
  RefreshTokenStrategy,
} from './strategies';

@Module({
  imports: [
    forwardRef(() => UserModule),
    PassportModule.register({
      defaultStrategy: ['access-token', 'refresh-token'],
    }),
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
          verifyOptions: {
            algorithms: ['RS256'],
          },
        };
      },
    }),
    forwardRef(() => NotificationModule),
  ],
  controllers: [AuthController],
  providers: [AuthService, JwtStrategy, RefreshTokenStrategy, PublicStrategy],
  exports: [AuthService, JwtModule],
})
export class AuthModule {}
