import { Controller, Logger, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';
import { CreateCourseDto } from "../dtos/course/create-course.dto";

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

  @Post()
  public async create(data: CreateCourseDto) {}
}
