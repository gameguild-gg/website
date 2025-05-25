import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { Int } from '@nestjs/graphql';
import { JobPostEntity } from '../job/entities/job-post.entity';
import { JobApplicationEntity } from '../job/entities/job-application.entity';

@Resolver(() => JobPostEntity)
export class JobResolver {
  @Query(() => [JobPostEntity], { name: 'jobPosts' })
  async findAllJobPosts(): Promise<JobPostEntity[]> {
    // TODO: Implement job service when available
    return [];
  }

  @Query(() => JobPostEntity, { name: 'jobPost', nullable: true })
  async findOneJobPost(@Args('id', { type: () => ID }) id: string): Promise<JobPostEntity | null> {
    // TODO: Implement job service when available
    return null;
  }

  @Query(() => [JobApplicationEntity], { name: 'jobApplications' })
  async findAllJobApplications(): Promise<JobApplicationEntity[]> {
    // TODO: Implement job service when available
    return [];
  }

  @Query(() => Int, { name: 'jobPostCount' })
  async jobPostCount(): Promise<number> {
    // TODO: Implement job service when available
    return 0;
  }
}
