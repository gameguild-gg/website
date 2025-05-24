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
import { APP_INTERCEPTOR } from '@nestjs/core';
import { RequireRoleInterceptor } from '../auth/interceptors/require-role.interceptor';
import { ProjectVersionController } from './project-version.controller';
import { ProjectVersionService } from './project-version.service';
import { TicketEntity } from './entities/ticket.entity';
import { TicketController } from './ticket.controller';
import { TicketService } from './ticket.service';
import { CoursesController } from './courses.controller';
import { CourseService } from './courses.service';
import { QuizEntity } from './entities/quiz.entity';
import { QuizController } from './quiz.controller';
import { QuizService } from './quiz.service';

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
      TicketEntity,
      QuizEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [ContentController, ProjectController, ProjectVersionController, TicketController, CoursesController, QuizController],
  providers: [ContentService, ProjectService, ProjectVersionService, TicketService, CourseService, QuizService],
  exports: [ContentService, ProjectService, ProjectVersionService, TicketService, CourseService, QuizService],
})
export class ContentModule {}
