import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserModule } from '../user/user.module';
import { ContentModule } from '../cms/content.module';
import { JobPostEntity } from './entities/job-post.entity';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { JobTagEntity } from './entities/job-tag.entity';
import { JobController } from './job.controller';
import { JobService } from './job.service';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      JobPostEntity,
      JobAplicationEntity,
      JobTagEntity,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [JobController],
  providers: [JobService],
  exports: [JobService],
})
export class JobModule {}
