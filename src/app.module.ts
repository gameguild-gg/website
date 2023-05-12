import { Module } from '@nestjs/common';
import { RenderModule } from 'nest-next';
import Next from 'next';
import { AppController } from './app.controller';
import { BlogController } from './common/blog/blog.controller';
import { BlogService } from './common/blog/blog.service';
import {ProposalModule} from "./modules/proposal/proposal.module";
import {TypeOrmModule} from "@nestjs/typeorm";
import {SharedModule} from "./shared/shared.module";
import {ApiConfigService} from "./shared/config.service";
import {ConfigModule} from "@nestjs/config";

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true }),
    TypeOrmModule.forRootAsync({
      imports: [SharedModule],
      useFactory: (configService: ApiConfigService) =>
          configService.postgresConfig,
      inject: [ApiConfigService],
    }),
    RenderModule.forRootAsync(
      Next({
        dev: process.env.NODE_ENV !== 'production',
        conf: { useFilesystemPublicRoutes: false },
      }),
    ),
    ProposalModule,
  ],
  controllers: [AppController, BlogController],
  providers: [BlogService],
})
export class AppModule {}
