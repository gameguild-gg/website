import { Module } from '@nestjs/common';

import { TypeOrmModule } from '@nestjs/typeorm';
import { SharedModule } from './shared/shared.module';
import { ApiConfigService } from './shared/config.service';
import { ConfigModule } from '@nestjs/config';
import { UserProfileModule } from './modules/user/user-profile.module';
import { AuthModule } from './modules/auth/auth.module';
import { UploadModule } from './modules/upload/upload.module';
import { ChapterModule } from './modules/chapter/chapter.module';
import { CourseModule } from './modules/course/course.module';
import { PostModule } from './modules/post/post.module';
import { EventModule } from './modules/event/event.module';
import { UserModule } from './modules/user/user.module';
import { ProposalModule } from './modules/proposal/proposal.module';
import { RootModule } from './modules/root/root.module';

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true }),
    TypeOrmModule.forRootAsync({
      imports: [SharedModule],
      useFactory: (configService: ApiConfigService) =>
        configService.postgresConfig,
      inject: [ApiConfigService],
    }),
    AuthModule,
    UserModule,
    UserProfileModule,
    UploadModule,
    ChapterModule,
    CourseModule,
    PostModule,
    EventModule,
    ProposalModule,
    RootModule,
  ],
  controllers: [],
  providers: [],
})
export class AppModule {}
