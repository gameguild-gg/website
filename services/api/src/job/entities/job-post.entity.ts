import { Column, Entity, JoinTable, OneToMany } from 'typeorm';
import { ContentBase } from '../../cms/entities/content.base';
import { JobTagEntity } from './job-tag.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity({ name: 'job-post' })
export class JobPostEntity extends ContentBase {
  // a job is very similar to a content base, but has many job tags
  @OneToMany(() => JobTagEntity, (jobTag) => jobTag.job)
  @ApiProperty({ type: JobTagEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => JobTagEntity)
  @JoinTable()
  tags: JobTagEntity[];
}
