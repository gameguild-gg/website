import { ApiProperty } from '@nestjs/swagger';
import { JobPostEntity } from '../entities/job-post.entity';

export class JobPostWithAppliedDto extends JobPostEntity {
    
  // Applied
  @ApiProperty({ required: true, default: false })
  applied: boolean;

}
