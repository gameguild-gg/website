import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobPostEntity } from './entities/job-post.entity';
import { WithRolesService } from '../cms/with-roles.service';

@Injectable()
export class JobPostService extends WithRolesService<JobPostEntity> {

  private readonly logger = new Logger(JobPostService.name);

  constructor(
    @InjectRepository(JobPostEntity)
    private readonly jobPostRepository: Repository<JobPostEntity>,
  ) {
    super(jobPostRepository)
  }

}
