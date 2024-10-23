import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobApplicationEntity } from './entities/job-application.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { JobPostEntity } from './entities/job-post.entity';
import { UserEntity } from '../user/entities';

@Injectable()
export class JobApplicationService extends TypeOrmCrudService<JobApplicationEntity> {
  private readonly logger = new Logger(JobApplicationService.name);

  constructor(
    @InjectRepository(JobApplicationEntity)
    private readonly jobAplicationRepository: Repository<JobApplicationEntity>,
  ) {
    super(jobAplicationRepository);
  }

  async advanceCandidate(application: JobApplicationEntity): Promise<JobApplicationEntity> {
    if (application.progress >= 5 || application.rejected){
      throw new Error('Invalid application for advancing');
    }
    application.progress += 1;
    return this.repo.save(application);
  }

  async undoAdvanceCandidate(application: JobApplicationEntity): Promise<JobApplicationEntity> {
    if (application.progress <= 0 || application.rejected){
      throw new Error('Invalid application for undoing advance');
    }
    application.progress -= 1;
    return this.repo.save(application);
  }

  async rejectCandidate(application: JobApplicationEntity): Promise<JobApplicationEntity> {
    if (application.rejected){
      throw new Error('Invalid application for rejecting');
    }
    application.rejected = true;
    return this.repo.save(application);
  }

  async undoRejectCandidate(application: JobApplicationEntity): Promise<JobApplicationEntity> {
    if (!application.rejected){
      throw new Error('Invalid application for undoing rejection');
    }
    application.rejected = false;
    return this.repo.save(application);
  }
}
