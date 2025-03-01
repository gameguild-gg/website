import { ApiProperty } from '@nestjs/swagger';
import { IsArray } from 'class-validator';
import { JoinTable } from 'typeorm';
import { Type } from 'class-transformer';
import { JobPostEntity } from '../entities/job-post.entity';

export class JobPostCreateDto extends JobPostEntity {
  // Job Tags (as string array)
  @ApiProperty({ type: String, isArray: true })
  @IsArray({ message: 'error.IsArray: job_tag_ids should be an array' })
  @Type(() => String)
  @JoinTable()
  readonly job_tag_ids: string[];
}
