import { ApiProperty } from '@nestjs/swagger';
import { JobPostEntity } from '../entities/job-post.entity';
import { Type } from 'class-transformer';
import { MaxLength, IsNotEmpty, IsString, IsOptional, IsArray, ValidateNested } from 'class-validator';
import { Column, Index, ManyToMany, JoinTable } from 'typeorm';
import { JobTagEntity } from '../entities/job-tag.entity';
import { JobTypeEnum } from '../entities/job-type.enum';

export class JobPostWithAppliedDto {

   // Location
   @Index({ unique: false })
   @ApiProperty({ nullable: false, default: 'Remote', type: 'varchar' })
   @MaxLength(64, { message: 'error.maxLength: location is too long, max 64' })
   @IsNotEmpty({ message: 'error.isNotEmpty: location is required' })
   @IsString({ message: 'error.isString: location must be a string' })
   location: string;
 
   @Index({ unique: false })
   @ApiProperty({ type: 'enum', enum: JobTypeEnum, default: JobTypeEnum.TASK, nullable: false,})
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
