import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ContentController } from './content.controller';
import { ContentService } from './content.service';
import { CourseEntity } from './entities/course.entity';
import { LectureEntity } from './entities/lecture.entity';
import { ChapterEntity } from './entities/chapter.entity';
import { UserModule } from '../user/user.module';
import { PostEntity } from './entities/post.entity';
import { ProjectEntity } from './entities/project.entity';
import { ProjectVersionEntity } from './entities/project-version.entity';
import { ProjectFeedbackResponseEntity } from './entities/project-feedback-response.entity';
import { GameController } from './game.controller';
import { GameService } from './game.service';
import { APP_INTERCEPTOR } from '@nestjs/core';
import { RequireRoleInterceptor } from '../auth/interceptors/require-role.interceptor';
import { GameVersionController } from './game-version.controller';
import { GameVersionService } from './game-version.service';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      CourseEntity,
      LectureEntity,
      ChapterEntity,
      PostEntity,
      ProjectEntity,
      ProjectVersionEntity,
      ProjectFeedbackResponseEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [ContentController, GameController, GameVersionController],
  providers: [ContentService, GameService, GameVersionService],
  exports: [ContentService, GameService, GameVersionService],
})
export class ContentModule {}
