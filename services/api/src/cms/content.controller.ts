import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

  // @Post('course/create')
  // @Auth(AuthenticatedRoute) // todo: anyone can create?
  // @ApiResponse({ type: CourseEntity })
  // public async createEmptyCourse(@AuthUser() user: UserEntity): Promise<CourseEntity> {
  //   return this.courseService.createEmptyCourse(user);
  // }
}
