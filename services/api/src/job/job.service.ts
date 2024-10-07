import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobPostEntity } from './entities/job-post.entity';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { JobTagEntity } from './entities/job-tag.entity';
import { UserEntity } from '../user/entities';
import { UserService } from '../user/user.service';

@Injectable()
export class JobService {
  private readonly logger = new Logger(JobService.name);

  constructor(
    @InjectRepository(JobPostEntity)
    private readonly jobPostRepository: Repository<JobPostEntity>,
    @InjectRepository(JobAplicationEntity)
    private readonly jobAplicationRepository: Repository<JobAplicationEntity>,
    @InjectRepository(JobTagEntity)
    private readonly jobTagRepository: Repository<JobTagEntity>,
    private readonly userService: UserService,
  ) {}

  async createJobPost(user: UserEntity): Promise<JobPostEntity> {
    let jobPost = new JobPostEntity();
    user = await this.userService.findOne({ where: { id: user.id } });
    jobPost.owner = user;
    jobPost = await this.jobPostRepository.save(jobPost);

    return this.jobPostRepository.findOne({
      where: { id: jobPost.id },
    });
  }
}
