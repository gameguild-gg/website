import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserModule } from '../user/user.module';
import { JobPostEntity } from './entities/job-post.entity';
import { JobTagEntity } from './entities/job-tag.entity';
import { JobApplicationEntity } from './entities/job-application.entity';
import { JobPostController } from './job-post.controller';
import { JobTagController } from './job-tag.controller';
import { JobApplicationController } from './job-application.controller';
import { JobPostService } from './job-post.service';
import { JobTagService } from './job-tag.service';
import { JobApplicationService } from './job-application.service';

@Module({
  imports: [TypeOrmModule.forFeature([JobPostEntity, JobTagEntity, JobApplicationEntity]), forwardRef(() => UserModule)],
  controllers: [JobPostController, JobTagController, JobApplicationController],
  providers: [JobPostService, JobTagService, JobApplicationService],
  exports: [JobPostService, JobTagService, JobApplicationService],
})
export class JobModule {}
