import { MiddlewareConsumer, Module, NestModule } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { AuthService } from './auth.service';
import { UsersModule } from '../users/users.module';
import { AuthController } from './auth.controller';
import { LocalStrategy } from './strategies/local.strategy';
import { PassportModule } from '@nestjs/passport';
import { JwtStrategy } from './strategies/jwt.strategy';
import { ConfigService } from '@nestjs/config';
import { SignUpValidationMiddleware } from './middlewares/SignUpValidation.middleware';

@Module({
  imports: [
    UsersModule, // forwardRef(() => UserModule),
    PassportModule, // PassportModule.register({ defaultStrategy: 'jwt' }),
    JwtModule.register({
      privateKey: process.env.PRIVATE_KEY,
      publicKey: process.env.PUBLIC_KEY,
      signOptions: {
        algorithm: 'RS256',
        expiresIn: '30d',
      },
    }),
  ],
  providers: [AuthService, LocalStrategy, JwtStrategy],
  controllers: [AuthController],
  exports: [AuthService],
})
export class AuthModule implements NestModule {
  configure(consumer: MiddlewareConsumer) {
    consumer.apply(SignUpValidationMiddleware).forRoutes('auth/sign-up');
  }
}
