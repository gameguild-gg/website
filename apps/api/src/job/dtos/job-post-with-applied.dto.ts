import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsNotEmpty, IsOptional, IsString, MaxLength, ValidateNested } from 'class-validator';
import { Index, JoinTable, ManyToMany } from 'typeorm';
import { JobTagEntity } from '../entities/job-tag.entity';
import { JobTypeEnum } from '../entities/job-type.enum';

export class JobPostWithAppliedDto {
  // Location
  @Index({ unique: false })
  @ApiProperty({ nullable: false, default: 'Remote', type: 'string' })
  @MaxLength(64, { message: 'error.maxLength: location is too long, max 64' })
  @IsNotEmpty({ message: 'error.isNotEmpty: location is required' })
  @IsString({ message: 'error.isString: location must be a string' })
  location: string;

  @Index({ unique: false })
  @ApiProperty({
    enum: JobTypeEnum,
    default: JobTypeEnum.TASK,
    nullable: false,
  })
  job_type: JobTypeEnum;

  // Tags
  @ApiProperty({ type: JobTagEntity, isArray: true })
  @IsOptional()
  @ManyToMany(() => JobTagEntity, (jobTag) => jobTag.id)
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => JobTagEntity)
  @JoinTable()
  job_tags?: JobTagEntity[];

  // Applied
  @ApiProperty({ required: true, default: false })
  applied: boolean;
}
