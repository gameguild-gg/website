import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { JobTagEntity } from './entities/job-tag.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class JobTagService extends TypeOrmCrudService<JobTagEntity> {
  private readonly logger = new Logger(JobTagService.name);

  constructor(
    @InjectRepository(JobTagEntity)
    private readonly jobTagRepository: Repository<JobTagEntity>,
  ) {
    super(jobTagRepository);
  }
}
