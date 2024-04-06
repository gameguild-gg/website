import { Body, Controller, Delete, Get, Logger, Param, Patch, Post } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';
import { AuthUser } from '../auth';
import { Auth } from '../auth/decorators/http.decorator';

import { UserEntity } from '../user/entities';
import { CourseEntity } from './entities/course.entity';
import { ChapterEntity } from './entities/chapter.entity';
import { LectureEntity } from './entities/lecture.entity';

import { CreateCourseDto } from '../dtos/course/create-course.dto';
import { UpdateCourseDto } from '../dtos/course/update-course.dto';
import { CreateChapterDto } from '../dtos/chapter/create-chapter.dto';
import { UpdateChapterDto } from '../dtos/chapter/update-chapter.dto';
import { CreateLectureDto } from '../dtos/lecture/create-lecture.dto';
import { UpdateLectureDto } from '../dtos/lecture/update-lecture.dto';

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

  //#region Courses
  @Post('course/create-empty')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async createEmptyCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.createEmptyCourse(user);
  }

  @Post('course/create')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async createCourse(
    @AuthUser() user: UserEntity,
    @Body() course: CreateCourseDto,
  ): Promise<CourseEntity> {
    return this.courseService.createCourse(user, course);
  }

  @Post('course/get-all')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async getAllCourses(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.getAllCourses();
  }

  @Get('course/get/:id')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async getCourse(
    @AuthUser() user: UserEntity,
    @Param('id') id: string,
  ): Promise<CourseEntity> {
    return this.courseService.getCourse(user, id);
  }

  @Patch('course/update')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async updateCourse(
    @AuthUser() user: UserEntity,
    @Body() update: UpdateCourseDto,
  ): Promise<CourseEntity> {
    return this.courseService.updateCourse(user, update);
  }

  @Delete('course/delete')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async deleteCourse(
    @AuthUser() user: UserEntity,
    @Body() id: string,
  ): Promise<CourseEntity> {
    return this.courseService.deleteCourse(user, id);
  }
  //#endregion
  
  //#region Chapters
  @Post('chapter/create-empty')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async createEmptyChapter(
    @AuthUser() user: UserEntity,
  ): Promise<ChapterEntity> {
    return this.courseService.createEmptyChapter(user);
  }

  @Post('chapter/create')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async createChapter(
    @AuthUser() user: UserEntity,
    @Body() course: CreateChapterDto,
  ): Promise<ChapterEntity> {
    return this.courseService.createChapter(user, course);
  }

  @Get('chapter/get-all/:id')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async getAllChapter(
    @AuthUser() user: UserEntity,
    @Param('id') courseId: string,
  ): Promise<ChapterEntity[]> {
    return this.courseService.getAllChapters(user, courseId);
  }

  @Get('chapter/get/:id')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async getChapter(
    @AuthUser() user: UserEntity,
    @Param('id') chapterId: string,
  ): Promise<ChapterEntity> {
    return this.courseService.getChapter(user, chapterId);
  }

  @Patch('chapter/update')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async updateChapter(
    @AuthUser() user: UserEntity,
    @Body() update: UpdateChapterDto,
  ): Promise<ChapterEntity> {
    return this.courseService.updateChapter(user, update);
  }

  @Delete('chapter/delete')
  @Auth()
  @ApiOkResponse({ type: ChapterEntity })
  public async deleteChapter(
    @AuthUser() user: UserEntity,
    @Body() id: string,
  ): Promise<ChapterEntity> {
    return this.courseService.deleteChapter(user, id);
  }
  //#endregion

  //#region Lectures
  @Post('lecture/create-empty')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async createEmptyLecture(
    @AuthUser() user: UserEntity,
  ): Promise<LectureEntity> {
    return this.courseService.createEmptyLecture(user);
  }

  @Post('lecture/create')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async createLecture(
    @AuthUser() user: UserEntity,
    @Body() lecture: CreateLectureDto,
  ): Promise<LectureEntity> {
    return this.courseService.createLecture(user, lecture);
  }

  @Post('lecture/get-all')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async getAllLectures(
    @AuthUser() user: UserEntity,
    @Body() chapterId: string,
  ): Promise<LectureEntity[]> {
    return this.courseService.getAllLectures(user, chapterId);
  }

  @Get('lecture/get/:id')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async getLecture(
    @AuthUser() user: UserEntity,
    @Param('id') id: string,
  ): Promise<LectureEntity> {
    return this.courseService.getLecture(id);
  }

  @Patch('lecture/update')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async updateLecture(
    @AuthUser() user: UserEntity,
    @Body() update: UpdateLectureDto,
  ): Promise<LectureEntity> {
    return this.courseService.updateLecture(update);
  }

  @Delete('lecture/delete')
  @Auth()
  @ApiOkResponse({ type: LectureEntity })
  public async deleteLecture(
    @AuthUser() user: UserEntity,
    @Body() id: string,
  ): Promise<LectureEntity> {
    return this.courseService.deleteLecture(id);
  }
  //#endregion
}
