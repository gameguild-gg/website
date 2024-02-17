import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_GUARD } from '@nestjs/core';
import { TypeOrmModule } from '@nestjs/typeorm';

import { AppController } from './app.controller';
import { AuthModule } from './auth/auth.module';
import { CommonModule } from './common/common.module';
import { ApiConfigService } from './common/config.service';
import { CourseModule } from './course/course.module';
import { EventModule } from './event/event.module';
import { NotificationModule } from './notification/notification.module';
import { ProposalModule } from './proposal/proposal.module';
import { UserModule } from './user/user.module';
import { CompetitionModule } from './competition/competition.module';
import { HttpAdapterHost } from '@nestjs/core';
import { ClsModule } from "nestjs-cls";

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
    CourseModule,
    ProposalModule,
    EventModule,
    CompetitionModule,
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
