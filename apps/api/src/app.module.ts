import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_GUARD } from '@nestjs/core';
import { CqrsModule } from '@nestjs/cqrs';
import { TypeOrmModule } from '@nestjs/typeorm';

import { AuthModule } from '@/auth/auth.module';
import { AccessTokenGuard } from '@/auth/guards/access-token.guard';
import { UserModule } from '@/user/user.module';

import { AppController } from './app.controller';
import { appConfig } from './config/app.config';
import { typeormConfig } from './config/typeorm.config';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: ['.env', '.env.local'],
      load: [appConfig, typeormConfig],
    }),
    CqrsModule.forRoot(),
    TypeOrmModule.forRootAsync(typeormConfig.asProvider()),
    AuthModule,
    UserModule,
    // fix auth interceptor and guard to store user in context
    // ClsModule.forRoot({
    //   global: true,
    //   middleware: {
    //     mount: true,
    //   },
    // }),
    //   CacheModule.registerAsync({
    //     // todo: add redis cache when scaling
    //     isGlobal: true, // globally available
    //     useFactory: (configService: ApiConfigService) => ({
    //       store: 'memory',
    //       ttl: 5 * 60 * 1000, // 5 minutes
    //       max: 10000, // 10k requests in 5 minutes is a nice limit
    //     }),
    //   }),

    //   // NotificationModule,
    //   // ContentModule,
    //   // ProposalModule,
    //   // EventModule,
    //   // CompetitionModule,
    //   // HealthcheckModule,
    //   // TagModule,
    //   // JobModule,
    //   // AssetModule,
    //   // MulterModule.register({
    //   //   storage: diskStorage({
    //   //     destination: '/tmp/uploads',
    //   //     filename: (req, file, cb) => {
    //   //       const filename = `${Date.now()}-${file.originalname}`;
    //   //       cb(null, filename);
    //   //     },
    //   //   }),
    //   // }),
  ],
  controllers: [AppController],
  providers: [
    {
      provide: APP_GUARD,
      useClass: AccessTokenGuard,
    },
    // {
    //   provide: APP_INTERCEPTOR,
    //   useClass: AuthenticatedUserInterceptor,
    // },
  ],
})
export class AppModule {}
