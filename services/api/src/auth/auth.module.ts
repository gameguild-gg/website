import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { NotificationModule } from '../notification/notification.module';
import { UserModule } from '../user/user.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { JwtStrategy, LocalStrategy } from './strategies';

@Module({
  imports: [
    JwtModule.register({
      global: true,
      secret: 'secret',
      // privateKey: process.env.PRIVATE_KEY,
      // publicKey: process.env.PUBLIC_KEY,
      signOptions: {
        algorithm: 'HS512',
        expiresIn: '15m',
        // TODO: get this from config service.
        // expiresIn: configService.getNumber('JWT_TOKEN_EXPIRATION_TIME'),
      },
      verifyOptions: {
        algorithms: ['HS512'],
        ignoreExpiration: false,
      },
    }),
    // TODO: get this from config service.
    // JwtModule.registerAsync({
    //   imports: [ApiConfigService],
    //   inject: [ApiConfigService],
    //   useFactory: (configService: ApiConfigService) => {
    //     return {
    //       privateKey: process.env.PRIVATE_KEY,
    //       publicKey: process.env.PUBLIC_KEY,
    //       signOptions: {
    //         algorithm: 'RS256',
    //         expiresIn: configService.getNumber('JWT_TOKEN_EXPIRATION_TIME'),
    //       },
    //     };
    //   }
    // }),
    PassportModule, // PassportModule.register({ defaultStrategy: 'jwt' }),
    NotificationModule,
    UserModule, // forwardRef(() => UserModule),
  ],
  controllers: [AuthController],
  providers: [AuthService, LocalStrategy, JwtStrategy],
  exports: [AuthService],
})
export class AuthModule {}
