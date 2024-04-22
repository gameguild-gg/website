import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_GUARD } from '@nestjs/core';
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
import { HttpAdapterHost } from '@nestjs/core';
import { ClsModule } from "nestjs-cls";
import { HealthcheckModule } from './healthcheck/healthcheck.module';
import { TagModule } from './tag/tag.module';
import { IpfsModule } from './asset/ipfs.module';

@Module({
  imports: [
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
    IpfsModule,
  ],
  controllers: [AppController],
  providers: [
    // {
    //   provide: APP_GUARD,
    //   useClass: JwtAccessTokenGuard,
    // },
  ],
})
export class AppModule {}
