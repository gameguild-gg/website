import { Column, Entity, Index, JoinTable, ManyToMany } from 'typeorm';
import { ObjectType, Field } from '@nestjs/graphql';
import { ContentBase } from '../../cms/entities/content.base';
import { JobTagEntity } from './job-tag.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, IsOptional, IsString, MaxLength, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { JobTypeEnum } from './job-type.enum';

@Entity({ name: 'job_post' })
@ObjectType()
export class JobPostEntity extends ContentBase {
  // Location
  @Column({ length: 256, nullable: false, default: 'Remote', type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty()
  @MaxLength(64, { message: 'error.maxLength: location is too long, max 64' })
  @IsNotEmpty({ message: 'error.isNotEmpty: location is required' })
  @IsString({ message: 'error.isString: location must be a string' })
  @Field()
  location: string;

  @Index({ unique: false })
  @Column({
    type: 'enum',
    enum: JobTypeEnum,
    default: JobTypeEnum.TASK,
    nullable: false,
  })
  @ApiProperty({ enum: JobTypeEnum })
  @Field(() => JobTypeEnum)
  job_type: JobTypeEnum;

  // Tags
  @ApiProperty({ type: JobTagEntity, isArray: true })
  @IsOptional()
  @ManyToMany(() => JobTagEntity, (jobTag) => jobTag.id)
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => JobTagEntity)
  @JoinTable()
  @Field(() => [JobTagEntity], { nullable: true })
  job_tags?: JobTagEntity[];
}
