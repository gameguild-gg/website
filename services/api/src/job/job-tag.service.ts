import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
// import { JobPostEntity } from './entities/job-post.entity';
// import { JobAplicationEntity } from './entities/job-aplication.entity';
import { JobTagEntity } from './entities/job-tag.entity';
// import { UserEntity } from '../user/entities';
// import { UserService } from '../user/user.service';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
// import { WithRolesService } from '../cms/with-roles.service';

@Injectable()
export class JobTagService extends TypeOrmCrudService<JobTagEntity> {

  private readonly logger = new Logger(JobTagService.name);

  constructor(
    @InjectRepository(JobTagEntity)
    private readonly jobTagRepository: Repository<JobTagEntity>,
  ) {
    super(jobTagRepository)
  }

}
