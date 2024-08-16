import { Controller, Logger, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { ContentService } from './content.service';
import { AuthUser } from '../auth';
import { Auth } from '../auth/decorators/http.decorator';
import { UserEntity } from '../user/entities';
import { CourseEntity } from './entities/course.entity';
import { OkResponse } from '../common/decorators/return-type.decorator';
import { AuthType } from '../auth/guards';
import { AuthenticatedRoute } from '../auth/auth.enum';

@Controller('content')
@ApiTags('content')
export class ContentController {
  private readonly logger = new Logger(ContentController.name);

  constructor(private readonly courseService: ContentService) {}

  @Post('course/create')
  @Auth(AuthenticatedRoute) // todo: anyone can create?
  @OkResponse({ type: CourseEntity })
  public async createEmptyCourse(
    @AuthUser() user: UserEntity,
  ): Promise<CourseEntity> {
    return this.courseService.createEmptyCourse(user);
  }
}
