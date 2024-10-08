import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserModule } from '../user/user.module';
import { ContentModule } from '../cms/content.module';
import { JobPostEntity } from './entities/job-post.entity';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { JobTagEntity } from './entities/job-tag.entity';
import { JobPostController } from './job-post.controller';
import { JobPostService } from './job-post.service';
import { JobTagController } from './job-tag.controller';
import { JobTagService } from './job-tag.service';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      JobPostEntity,
      JobAplicationEntity,
      JobTagEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [JobPostController, JobTagController],
  providers: [JobPostService, JobTagService],
  exports: [JobPostService, JobTagService],
})
export class JobModule {}
