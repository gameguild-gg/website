import { Module } from '@nestjs/common';
import { CacheModule } from '@nestjs/cache-manager';
import { ConfigModule } from '@nestjs/config';
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
import { JobModule } from './job/job.module';
import { ClsModule } from 'nestjs-cls';
import { DataSource } from 'typeorm';
import { AssetModule } from './asset';
import { MulterModule } from '@nestjs/platform-express';
import { diskStorage } from 'multer';
import { SchemaDumpService } from './common/db-schema-dump-service';
import { CleanupService } from './common/cleanup-unused-db-tables';
import { ProgramModule } from './program/program.module';
import { ProductModule } from './product/product.module';
import { KycModule } from './kyc/kyc.module';
import { FinancialModule } from './financial/financial.module';

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true }),
    CommonModule,
    TypeOrmModule.forRootAsync({
      inject: [ApiConfigService],
      useFactory: (configService: ApiConfigService) => configService.postgresConfig,
    }),
    CacheModule.registerAsync({
      isGlobal: true,
      useFactory: (configService: ApiConfigService) => ({
        store: 'memory',
        ttl: 5 * 60 * 1000,
        max: 10000,
      }),
    }),
    MulterModule.register({
      storage: diskStorage({
        destination: '/tmp/uploads',
        filename: (req, file, cb) => {
          const filename = `${Date.now()}-${file.originalname}`;
          cb(null, filename);
        },
      }),
    }),
    ClsModule.forRoot({
      global: true,
      middleware: {
        mount: true,
      },
    }),
    AuthModule,
    UserModule,
    NotificationModule,
    ContentModule,
    ProposalModule,
    EventModule,
    CompetitionModule,
    HealthcheckModule,
    JobModule,
    AssetModule,
    ProgramModule,
    ProductModule,
    KycModule,
    FinancialModule,
  ],
  controllers: [AppController],
  providers: [
    {
      provide: 'DataSource',
      useFactory: (dataSource: DataSource) => dataSource,
      inject: [DataSource],
    },
    SchemaDumpService,
    CleanupService,
  ],
})
export class AppModule {}
