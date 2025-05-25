import { Column, Entity, ManyToOne } from 'typeorm';
import { ObjectType, Field } from '@nestjs/graphql';
import { JobPostEntity } from './job-post.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsInt, IsNotEmpty } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';

@Entity({ name: 'job_application' })
@ObjectType()
export class JobApplicationEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { nullable: false, eager: false })
  @Field(() => UserEntity)
  applicant: UserEntity;

  @ApiProperty({ type: () => JobPostEntity })
  @ManyToOne((job) => JobPostEntity, (jobPost) => jobPost.id)
  @Field(() => JobPostEntity)
  job: JobPostEntity;

  @ApiProperty({ nullable: false, type: 'integer', default: 0 })
  @Column({ nullable: false, type: 'integer', default: 0 })
  @IsNotEmpty({ message: 'error.isNotEmpty: progress is required' })
  @IsInt({ message: 'error.IsInt: progress must be an int' })
  @Field()
  progress: number;

  @Column({ nullable: false, type: 'boolean', default: false })
  @ApiProperty({ nullable: false, type: 'boolean', default: false })
  @IsNotEmpty({ message: 'error.isNotEmpty: rejected is required' })
  @IsBoolean({ message: 'error.IsBoolean: rejected must be a boolean' })
  @Field()
  rejected: boolean;

  @Column({ nullable: false, type: 'boolean', default: false })
  @ApiProperty({ nullable: false, type: 'boolean', default: false })
  @IsNotEmpty({ message: 'error.isNotEmpty: withdrawn is required' })
  @IsBoolean({ message: 'error.IsBoolean: withdrawn must be a boolean' })
  @Field()
  withdrawn: boolean;
}
