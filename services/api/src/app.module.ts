import { Module } from '@nestjs/common';
import { CacheModule } from '@nestjs/cache-manager';
import { ConfigModule } from '@nestjs/config';
import { APP_GUARD, APP_INTERCEPTOR } from '@nestjs/core';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AppController } from './app.controller';
import { AuthModule } from './auth/auth.module';
import { CommonModule } from './common/common.module';
import { ApiConfigService } from './common/config.service';
import { ContentModule } from './cms/content.module';
import { EventModule } from './event/event.module';
import { NotificationModule } from './notification/notification.module';
import { ProposalModule } from './proposal/proposal.module';
import { UserModule } from './user/user.module';
import { CompetitionModule } from './competition/competition.module';
import { HealthcheckModule } from './healthcheck/healthcheck.module';
import { TagModule } from './tag/tag.module';
import { ClsModule } from 'nestjs-cls';
import { DataSource } from 'typeorm';

@Module({
  imports: [
    // fix auth interceptor and guard to store user in context
    ClsModule.forRoot({
      global: true,
      middleware: {
        mount: true,
      },
    }),
    ConfigModule.forRoot({ isGlobal: true }),
    TypeOrmModule.forRootAsync({
      imports: [CommonModule],
      inject: [ApiConfigService],
      useFactory: (configService: ApiConfigService) =>
        configService.postgresConfig,
    }),
    CacheModule.registerAsync({
      // todo: add redis cache when scaling
      isGlobal: true, // globally available
      useFactory: (configService: ApiConfigService) => ({
        store: 'memory',
        ttl: 5 * 60, // 5 minutes
        max: 10000, // 10k requests in 5 minutes is a nice limit
      }),
    }),
    CommonModule,
    AuthModule,
    UserModule,
    NotificationModule,
    ContentModule,
    ProposalModule,
    EventModule,
    CompetitionModule,
    HealthcheckModule,
    TagModule,
    // IpfsModule,
  ],
  controllers: [AppController],
  providers: [
    {
      provide: 'DataSource',
      useFactory: (dataSource: DataSource) => dataSource,
      inject: [DataSource],
    },
  ],
})
export class AppModule {}
