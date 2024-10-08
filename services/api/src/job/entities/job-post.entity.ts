import { Column, Entity, Index, JoinTable, OneToMany } from 'typeorm';
import { ContentBase } from '../../cms/entities/content.base';
import { JobTagEntity } from './job-tag.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, IsString, MaxLength, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity({ name: 'job-post' })
export class JobPostEntity extends ContentBase {

  // Location
  @Column({ length: 256, nullable: false, default: 'Remote', type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty()
  @MaxLength(64, { message: 'error.maxLength: location is too long, max 64' })
  @IsNotEmpty({ message: 'error.isNotEmpty: location is required' })
  @IsString({ message: 'error.isString: location must be a string' })
  location: string;

  // Tags
  @OneToMany(() => JobTagEntity, (jobTag) => jobTag.job)
  @ApiProperty({ type: JobTagEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => JobTagEntity)
  @JoinTable()
  tags: JobTagEntity[];
}
