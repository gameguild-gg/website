import { ApiProperty } from '@nestjs/swagger';
import {
  IsArray,
  IsNotEmpty,
  IsString,
  MaxLength,
  ValidateNested,
} from 'class-validator';
import { UserEntity } from '../../user/entities';
import { JoinTable, ManyToOne, OneToMany } from 'typeorm';
import { Type } from 'class-transformer';
import { JobPostEntity } from '../entities/job-post.entity';

export class JobPostCreateDto extends JobPostEntity{

  // Job Tags (as string array)
  @ApiProperty({ type: String, isArray: true })
  @IsArray({ message: 'error.IsArray: job_tag_ids should be an array' })
  @Type(() => String)
  @JoinTable()
  readonly job_tag_ids: string[];
  
}
