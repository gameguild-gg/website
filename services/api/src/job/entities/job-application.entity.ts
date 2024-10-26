import { Column, Entity } from 'typeorm';
import { ManyToOne } from 'typeorm';
import { JobPostEntity } from './job-post.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsInt, IsNotEmpty } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';

@Entity({ name: 'job-application' })
export class JobApplicationEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { nullable: false, eager: false })
  applicant: UserEntity;

  @ApiProperty({ type: () => JobPostEntity })
  @ManyToOne((job) => JobPostEntity, (jobPost) => jobPost.id)
  job: JobPostEntity;

  @ApiProperty({ nullable: false, type: 'integer', default: 0 } )
  @IsNotEmpty({ message: 'error.isNotEmpty: progress is required' })
  @IsInt({ message: 'error.IsInt: progress must be an int' })
  progress: number;

  @ApiProperty({ nullable: false, type: 'boolean', default: false })
  @IsNotEmpty({ message: 'error.isNotEmpty: rejected is required' })
  @IsBoolean({ message: 'error.IsBoolean: rejected must be a boolean' })
  rejected: boolean;
}
