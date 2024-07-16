import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ContentController } from './content.controller';
import { ContentService } from './content.service';
import { CourseEntity } from './entities/course.entity';
import { LectureEntity } from './entities/lecture.entity';
import { ChapterEntity } from './entities/chapter.entity';
import { UserModule } from '../user/user.module';
import { PostEntity } from './entities/post.entity';
import { GameEntity } from './entities/game.entity';
import { GameVersionEntity } from './entities/game-version.entity';
import { GameFeedbackResponseEntity } from './entities/game-feedback-response.entity';
import { GameController } from './game.controller';
import { GameService } from './game.service';
import { APP_INTERCEPTOR } from '@nestjs/core';
import { IsOwnerInterceptor } from '../common/interceptors/isowner.interceptor';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      CourseEntity,
      LectureEntity,
      ChapterEntity,
      PostEntity,
      GameEntity,
      GameVersionEntity,
      GameFeedbackResponseEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [ContentController, GameController],
  providers: [
    ContentService,
    GameService,
    {
      provide: APP_INTERCEPTOR,
      useClass: IsOwnerInterceptor,
    },
  ],
  exports: [ContentService, GameService],
})
export class ContentModule {}
