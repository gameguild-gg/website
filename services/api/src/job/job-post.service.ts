import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobPostEntity } from './entities/job-post.entity';
import { JobApplicationEntity } from './entities/job-application.entity';
import { JobPostWithAppliedDto } from './dtos/job-post-with-applied.dto';
import { WithRolesService } from '../cms/with-roles.service';
import { CrudRequest } from '@dataui/crud';

@Injectable()
export class JobPostService extends WithRolesService<JobPostEntity> {

  private readonly logger = new Logger(JobPostService.name);

  constructor(
    @InjectRepository(JobPostEntity)
    private readonly jobPostRepo: Repository<JobPostEntity>,
    @InjectRepository(JobApplicationEntity)
    private readonly jobApplicationRepo: Repository<JobApplicationEntity>,
  ) {
    super(jobPostRepo)
  }

  async getManyWithApplied(req: CrudRequest, userId: string): Promise<JobPostWithAppliedDto[]> {
    const jobPosts = await this.getMany(req) as JobPostEntity[];

    const jobApplications = await this.jobApplicationRepo.find({
      where: { applicant: { id: userId }, }, // TODO: get only the job aplications where the job posts obtained above are included
    });

    const appliedJobIds = jobApplications.map((application) => application.job.id);

    return jobPosts.map((jobPost) => ({
      ...jobPost,
      applied: appliedJobIds.includes(jobPost.id),
    }));
  }

}
