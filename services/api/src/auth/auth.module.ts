import {forwardRef, Module} from "@nestjs/common";
import {JwtModule} from '@nestjs/jwt';
import {PassportModule} from '@nestjs/passport';
import {CommonModule} from '../common/common.module';
import {ApiConfigService} from '../common/config.service';
import {NotificationModule} from '../notification/notification.module';
import {UserModule} from '../user/user.module';
import {AuthController} from './auth.controller';
import {AuthService} from './auth.service';
import {AccessTokenStrategy} from "./strategies/access-token.strategy";


@Module({
  imports: [

    PassportModule.register({defaultStrategy: 'access-token'}),
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
    forwardRef(() => NotificationModule),
    forwardRef(() => UserModule),
  ],
  controllers: [AuthController],
  providers: [AuthService, AccessTokenStrategy],
  exports: [AuthService, JwtModule],
})
export class AuthModule {
}
