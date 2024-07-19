import { Body, Controller, Logger, Post } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';
import { AuthUser } from '../auth';
import { Auth } from '../auth/decorators/http.decorator';
import { UserEntity } from '../user/entities';
import { CourseEntity } from './entities/course.entity';

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

  @Post('course/create')
  @Auth({ public: false }) // todo: anyone can create?
  @ApiOkResponse({ type: CourseEntity })
  public async createEmptyCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.createEmptyCourse(user);
  }
}
