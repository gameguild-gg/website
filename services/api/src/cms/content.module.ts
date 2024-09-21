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
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [
    ContentController,
    ProjectController,
    ProjectVersionController,
    TicketController,
  ],
  providers: [
    ContentService,
    ProjectService,
    ProjectVersionService,
    TicketService,
  ],
  exports: [
    ContentService,
    ProjectService,
    ProjectVersionService,
    TicketService,
  ],
})
export class ContentModule {}
