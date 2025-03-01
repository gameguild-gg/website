import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty } from 'class-validator';
import { ManyToOne } from 'typeorm';
import { JobPostEntity } from '../entities/job-post.entity';

export class JobAplicationCreateDto {
  // Job
  @ApiProperty({ type: () => JobPostEntity })
  @ManyToOne((job) => JobPostEntity, (jobPost) => jobPost.id)
  @IsNotEmpty({ message: 'error.invalidJob: Job must not be empty.' })
  job: JobPostEntity;
}
