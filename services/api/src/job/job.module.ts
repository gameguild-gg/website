import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserModule } from '../user/user.module';
import { JobPostEntity } from './entities/job-post.entity';
import { JobTagEntity } from './entities/job-tag.entity';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { JobPostController } from './job-post.controller';
import { JobTagController } from './job-tag.controller';
import { JobAplicationController } from './job-aplication.controller';
import { JobPostService } from './job-post.service';
import { JobTagService } from './job-tag.service';  
import { JobAplicationService } from './job-aplication.service';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      JobPostEntity,
      JobTagEntity,
      JobAplicationEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [JobPostController, JobTagController, JobAplicationController],
  providers: [JobPostService, JobTagService, JobAplicationService],
  exports: [JobPostService, JobTagService, JobAplicationService],
})
export class JobModule {}
