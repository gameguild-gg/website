import { Column, Entity, Index } from 'typeorm';
import { ManyToOne } from 'typeorm';
import { JobPostEntity } from './job-post.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsInt, IsNotEmpty, MaxLength } from 'class-validator';
import { EntityBase } from 'src/common/entities/entity.base';
import { UserEntity } from 'src/user/entities/user.entity';

@Entity({ name: 'job-aplication' })
export class JobAplicationEntity extends EntityBase{
    @Column()
    @ApiProperty({ type: () => UserEntity })
    @ManyToOne(() => UserEntity, { nullable: false, eager: false })
    aplicant: UserEntity;

    @Column()
    @ManyToOne((job) => JobPostEntity, (jobPost) => jobPost.tags)
    job: JobPostEntity;

    @Column({ nullable: false, type: 'int', default: 0 })
    @IsNotEmpty({ message: 'error.isNotEmpty: type is required' })
    @IsInt({ message: 'error.IsInt: type must be an int'})
    type: number;

    @Column({ nullable: false, type: 'int', default: 0 })
    @IsNotEmpty({ message: 'error.isNotEmpty: progress is required' })
    @IsInt({ message: 'error.IsInt: progress must be an int'})
    progress: number;
}
