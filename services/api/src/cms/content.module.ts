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
import { GameVersionController } from './game-version.controller';
import { GameVersionService } from './game-version.service';
import { CourseService } from './modules/course/course.service';

const providers = [
  ContentService,
  GameService,
  GameVersionService,
  CourseService,
];

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
  controllers: [ContentController, GameController, GameVersionController],
  providers,
  exports: [ ...providers ],
})
export class ContentModule {}
