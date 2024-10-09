import { Column, Entity, Index } from 'typeorm';
import { ManyToOne } from 'typeorm';
import { JobPostEntity } from './job-post.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsInt, IsNotEmpty, MaxLength } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';

@Entity({ name: 'job-aplication' })
export class JobAplicationEntity extends EntityBase{
    
    @ApiProperty({ type: () => UserEntity })
    @ManyToOne(() => UserEntity, { nullable: false, eager: false })
    aplicant: UserEntity;

    @ApiProperty({ type: () => JobPostEntity })
    @ManyToOne((job) => JobPostEntity, (jobPost) => jobPost.id)
    job: JobPostEntity;

    @Column({ nullable: false, type: 'int', default: 0 })
    @IsNotEmpty({ message: 'error.isNotEmpty: progress is required' })
    @IsInt({ message: 'error.IsInt: progress must be an int'})
    progress: number;
}
