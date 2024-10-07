import { Controller, Logger, Post } from '@nestjs/common';
import {
  ApiCreatedResponse,
  ApiOkResponse,
  ApiResponse,
  ApiTags,
} from '@nestjs/swagger';// ../cms/
import { ContentService } from '../cms/content.service';
import { JobService } from './job.service';
import { AuthUser } from '../auth';
import { Auth } from '../auth/decorators/http.decorator';
import { UserEntity } from '../user/entities';
import { JobPostEntity } from './entities/job-post.entity';
import { OkResponse } from '../common/decorators/return-type.decorator';
import { AuthType } from '../auth/guards';
import { AuthenticatedRoute } from '../auth/auth.enum';

@Controller('jobs')
@ApiTags('Jobs')
export class JobController {
  private readonly logger = new Logger(JobController.name);

  constructor(private readonly jobService: JobService) {}

  @Post('job-post/create')
  @Auth(AuthenticatedRoute) // todo: anyone can create?
  @ApiResponse({ type: JobPostEntity })
  public async createJobPost(
    @AuthUser() user: UserEntity,
  ): Promise<JobPostEntity> {
    return this.jobService.createJobPost(user);
  }


}
