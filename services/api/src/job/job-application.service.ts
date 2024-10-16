import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobApplicationEntity } from './entities/job-application.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { JobPostEntity } from './entities/job-post.entity';

@Injectable()
export class JobApplicationService extends TypeOrmCrudService<JobApplicationEntity> {

  private readonly logger = new Logger(JobApplicationService.name);

  constructor(
    @InjectRepository(JobApplicationEntity)
    private readonly jobAplicationRepository: Repository<JobApplicationEntity>,
  ) {
    super(jobAplicationRepository)
  }

  async checkIfUserAppliedToJobs(userId: string, jobPosts: JobPostEntity[]): Promise<boolean[]> {
    const jobIds = jobPosts.map(job => job.id);

    const appliedJobIds = await this.repo
      .createQueryBuilder()
      .select('JobApplicationEntity.job')
      .where('JobApplicationEntity.applicant = :userId', { userId })
      .andWhere('JobApplicationEntity.job IN (:...jobIds)', { jobIds })
      .getMany();

    const appliedJobIdsSet = new Set(appliedJobIds.map(app => app.job.id));

    return jobPosts.map(job => appliedJobIdsSet.has(job.id));
  }

}
