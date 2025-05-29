import { ForbiddenException, Injectable, Logger, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { In, Repository } from 'typeorm';
import { JobPostEntity } from './entities/job-post.entity';
import { JobApplicationEntity } from './entities/job-application.entity';
import { JobPostWithAppliedDto } from './dtos/job-post-with-applied.dto';
import { WithRolesService } from '../cms/with-roles.service';
import { CrudRequest, Override } from '@dataui/crud';
import { JobPostCreateDto } from './dtos/job-post-create.dto';
import { JobTagEntity } from './entities/job-tag.entity';
import { UserEntity } from 'src/user/entities';
import { JobPostWithApplicationsDto } from './dtos/job-post-with-applications.dto';

@Injectable()
export class JobPostService extends WithRolesService<JobPostEntity> {
  private readonly logger = new Logger(JobPostService.name);

  constructor(
    @InjectRepository(JobPostEntity)
    private readonly jobPostRepo: Repository<JobPostEntity>,
    @InjectRepository(JobApplicationEntity)
    private readonly jobApplicationRepo: Repository<JobApplicationEntity>,
    @InjectRepository(JobTagEntity)
    private readonly jobTagRepo: Repository<JobTagEntity>,
  ) {
    super(jobPostRepo);
  }

  @Override()
  async createOneJob(req: CrudRequest, body: JobPostCreateDto) {
    // Extract data
    const { job_tag_ids: job_tag_ids, ...jobPost } = body;
    // Create Job Post with no Tags
    const created_job_post = await this.createOne(req, jobPost as JobPostEntity);
    // Find Tags
    const job_tags = await this.jobTagRepo.find({
      where: {
        id: In(job_tag_ids),
      },
    });
    // Insert Tags
    await this.jobPostRepo.createQueryBuilder().relation(JobPostEntity, 'job_tags').of(created_job_post).addAndRemove(job_tags, []);
    return created_job_post;
  }

  async getManyWithApplied(req: CrudRequest, userId: string): Promise<JobPostWithAppliedDto[]> {
    const jobPosts = (await this.getMany(req)) as JobPostEntity[];

    const jobPostIds = jobPosts.map((jobPost) => jobPost.id);

    const jobApplications = await this.jobApplicationRepo.find({
      relations: { applicant: true, job: true },
      where: {
        applicant: { id: userId },
        job: { id: In(jobPostIds) },
      },
    });

    const appliedJobIds = jobApplications.map((application) => application.job.id);

    return jobPosts.map((jobPost) => ({
      ...jobPost,
      applied: appliedJobIds.includes(jobPost.id),
    }));
  }

  async getBySlug(slug: string): Promise<JobPostEntity> {
    return this.repo.findOne({ where: { slug }, relations: { owner: true } });
  }

  async getBySlugForOwner(slug: string, userId: string): Promise<JobPostWithApplicationsDto> {
    const jobPost = await this.repo.findOne({ where: { slug }, relations: { owner: true } });

    if (!jobPost) {
      throw new NotFoundException('Job post not found');
    }

    if (jobPost.owner.id !== userId) {
      throw new ForbiddenException('You are not the owner of this job post');
    }

    const applications = await this.jobApplicationRepo.find({ where: { job: jobPost } });

    return { jobPost, applications };
  }
}
