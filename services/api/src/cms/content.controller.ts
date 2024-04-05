import { Body, Controller, Delete, Get, Logger, Patch, Post } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';
import { CreateCourseDto } from '../dtos/course/create-course.dto';
import { AuthUser } from '../auth';
import { Auth } from '../auth/decorators/http.decorator';
import { UserEntity } from '../user/entities';
import { CourseEntity } from './entities/course.entity';

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

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

  @Get('course/get')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async getCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.getCourse();
  }

  @Patch('course/update')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async updateCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.updateCourse();
  }

  @Delete('course/delete')
  @Auth()
  @ApiOkResponse({ type: CourseEntity })
  public async deleteCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.deleteCourse();
  }
}
