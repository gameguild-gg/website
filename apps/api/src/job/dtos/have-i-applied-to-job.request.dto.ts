import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, ValidateNested } from 'class-validator';
import { JobPostEntity } from '../entities/job-post.entity';
import { Type } from 'class-transformer';

export class HaveIAppliedToJobRequestDto {
  // Job Posts
  @ApiProperty({ type: JobPostEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => JobPostEntity)
  @IsNotEmpty()
  jobPosts: JobPostEntity[];
}
