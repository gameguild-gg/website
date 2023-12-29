import { Controller, Logger, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CourseService } from './course.service';
import { CreateCourseDto } from './dtos/create-course.dto';

@Controller('courses')
@ApiTags('courses')
export class CourseController {
  private readonly logger = new Logger(CourseController.name);

  constructor(private readonly courseService: CourseService) {}

  @Post()
  public async create(data: CreateCourseDto) {}
}
