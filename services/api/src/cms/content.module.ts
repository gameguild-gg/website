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
import { ProjectController } from './project.controller';
import { ProjectService } from './project.service';
import { ProjectVersionController } from './project-version.controller';
import { ProjectVersionService } from './project-version.service';

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
  controllers: [ContentController, ProjectController, ProjectVersionController],
  providers: [ContentService, ProjectService, ProjectVersionService],
  exports: [ContentService, ProjectService, ProjectVersionService],
})
export class ContentModule {}
