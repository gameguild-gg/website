import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class JobAplicationService extends TypeOrmCrudService<JobAplicationEntity> {

  private readonly logger = new Logger(JobAplicationService.name);

  constructor(
    @InjectRepository(JobAplicationEntity)
    private readonly jobAplicationRepository: Repository<JobAplicationEntity>,
  ) {
    super(jobAplicationRepository)
  }

}
