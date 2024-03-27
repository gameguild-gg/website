import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ContentController } from './content.controller';
import { ContentService } from './content.service';
import { CourseEntity } from './entities/course.entity';
import { LectureEntity } from './entities/lecture.entity';
import { ChapterEntity } from './entities/chapter.entity';
import { UserModule } from '../user/user.module';
import { PostEntity } from './entities/post.entity';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      CourseEntity,
      LectureEntity,
      ChapterEntity,
      PostEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [ContentController],
  providers: [ContentService],
  exports: [ContentService],
})
export class ContentModule {}
