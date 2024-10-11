import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobApplicationEntity } from './entities/job-application.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class JobApplicationService extends TypeOrmCrudService<JobApplicationEntity> {

  private readonly logger = new Logger(JobApplicationService.name);

  constructor(
    @InjectRepository(JobApplicationEntity)
    private readonly jobAplicationRepository: Repository<JobApplicationEntity>,
  ) {
    super(jobAplicationRepository)
  }

}
