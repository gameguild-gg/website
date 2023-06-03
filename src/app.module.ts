import { Module } from '@nestjs/common';
import { RenderModule } from 'nest-next';
import Next from 'next';
import { AppController } from './app.controller';
import { BlogController } from './common/blog/blog.controller';
import { BlogService } from './common/blog/blog.service';
import {TypeOrmModule} from "@nestjs/typeorm";
import {SharedModule} from "./shared/shared.module";
import {ApiConfigService} from "./shared/config.service";
import {ConfigModule} from "@nestjs/config";
import { UserProfileModule } from './modules/user/user-profile.module';
import { AuthModule } from './modules/auth/auth.module';
import { UploadModule } from './modules/upload/upload.module';
import { ChapterModule } from './modules/chapter/chapter.module';
import { CourseModule } from './modules/course/course.module';
import { PostModule } from './modules/post/post.module';
import { EventModule } from './modules/event/event.module';
import {UserModule} from "./modules/user/user.module";
import {ProposalModule} from "./modules/proposal/proposal.module";

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
    UserModule,
    UserProfileModule,
    AuthModule,
    UploadModule,
    ChapterModule,
    CourseModule,
    PostModule,
    EventModule,
    ProposalModule,
  ],
  controllers: [AppController, BlogController],
  providers: [BlogService],
})
export class AppModule {}
