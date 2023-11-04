import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { CommonModule } from "../common/common.module";
import { ApiConfigService } from "../common/config.service";
import { NotificationModule } from '../notification/notification.module';
import { UserModule } from '../user/user.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { JwtStrategy, LocalStrategy } from './strategies';

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
    PassportModule.register({ defaultStrategy: 'jwt' }),
    NotificationModule,
    UserModule, // forwardRef(() => UserModule),
  ],
  controllers: [AuthController],
  providers: [AuthService, LocalStrategy, JwtStrategy],
  exports: [AuthService],
})
export class AuthModule {}
