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

  async userHasPermissionToManageApplication(applicationId:string, userId:string): Promise<boolean> {
    const app = await this.jobAplicationRepository.findOne({
      where: {id : applicationId},
      relations: ['job','job.owner']
    })
    if (app) {
      return true
    }
    return false
  }

  async myApplications(userId: string): Promise<JobApplicationEntity[]> {
    return this.jobAplicationRepository.find({
      where: { applicant: {id: userId } },
      relations: ['applicant','job','job.job_tags'],
    });
  }

  async myApplicationBySlug(slug:string, userId: string): Promise<JobApplicationEntity> {
    return this.jobAplicationRepository.findOne({
      where: {
        applicant: {id: userId },
        job: {slug: slug}
      },
      relations: ['applicant','job','job.job_tags'],
    });
  }

  async advanceCandidate(application: JobApplicationEntity, jobManagerId: string): Promise<JobApplicationEntity> {
    if (!await this.userHasPermissionToManageApplication(application.id, jobManagerId)) {
      throw new Error('You do not have the required permissions to manage this job application');
    }
    if (application.progress >= 5 || application.rejected){
      throw new Error('Invalid application for advancing');
    }
    application.progress += 1;
    return this.repo.save(application);
  }

  async undoAdvanceCandidate(application: JobApplicationEntity, jobManagerId: string): Promise<JobApplicationEntity> {
    if (!await this.userHasPermissionToManageApplication(application.id, jobManagerId)) {
      throw new Error('You do not have the required permissions to manage this job application');
    }
    if (application.progress <= 0 || application.rejected){
      throw new Error('Invalid application for undoing advance');
    }
    application.progress -= 1;
    return this.repo.save(application);
  }

  async rejectCandidate(application: JobApplicationEntity, jobManagerId: string): Promise<JobApplicationEntity> {
    if (!await this.userHasPermissionToManageApplication(application.id, jobManagerId)) {
      throw new Error('You do not have the required permissions to manage this job application');
    }
    if (application.rejected){
      throw new Error('Invalid application for rejecting');
    }
    application.rejected = true;
    return this.repo.save(application);
  }

  async undoRejectCandidate(application: JobApplicationEntity, jobManagerId: string): Promise<JobApplicationEntity> {
    if (!await this.userHasPermissionToManageApplication(application.id, jobManagerId)) {
      throw new Error('You do not have the required permissions to manage this job application');
    }
    if (!application.rejected){
      throw new Error('Invalid application for undoing rejection');
    }
    application.rejected = false;
    return this.repo.save(application);
  }
}
