import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_GUARD } from '@nestjs/core';
import { AppController } from './app.controller';
import { typeormConfig } from './config/typeorm.config';
import { appConfig } from './config/app.config';
import { CqrsModule } from '@nestjs/cqrs';
import { AuthModule } from '@/auth/auth.module';
import { AccessTokenGuard } from '@/auth/guards/access-token.guard';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserModule } from '@/user/user.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      load: [appConfig, typeormConfig],
      isGlobal: true,
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

    // TypeOrmModule.forRootAsync({
    //   imports: [CommonModule],
    //   inject: [ApiConfigService],
    //   useFactory: (configService: ApiConfigService) => configService.postgresConfig,
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
    //   // CommonModule,
    //   // AuthModule,
    //   // UserModule,
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
