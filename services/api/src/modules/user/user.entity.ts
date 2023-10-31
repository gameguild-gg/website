import {EntityBase} from '../../common/entity.base';
import {Column, Entity, Index, JoinColumn, OneToMany, OneToOne,} from 'typeorm';
import {UserRoleEnum} from './user-role.enum';
import {ApiProperty} from '@nestjs/swagger';
import {UserProfileEntity} from './user-profile.entity';
import {Exclude} from 'class-transformer';
import {CompetitionSubmissionEntity} from '../competition/entities/competition.submission.entity';

@Entity({name: 'user'})
export class UserEntity extends EntityBase {
    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    username: string;

    // todo: make it have multiple roles
    @Column({enum: UserRoleEnum, default: UserRoleEnum.COMMON})
    @ApiProperty({enum: UserRoleEnum})
    role: UserRoleEnum;

    // For email/pass login
    @Column({nullable: false, default: null})
    @ApiProperty()
    @Index()
    email: string;

    @Column({nullable: false, default: false})
    @ApiProperty()
    emailValidated: boolean;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Exclude()
    passwordHash: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Exclude()
    passwordSalt: string;

    // For social login
    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    facebookId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    googleId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    githubId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    appleId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    linkedinId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    twitterId: string;

    @Column({nullable: true, default: null})
    @ApiProperty()
    @Index()
    walletAddress: string;

    // relations
    @OneToOne(() => UserProfileEntity, (profile) => profile.user) // specify inverse side as a second parameter
    @JoinColumn()
    profile: UserProfileEntity;

    @OneToMany(() => CompetitionSubmissionEntity, (submission) => submission.user)
    competitionSubmissions: CompetitionSubmissionEntity[];
}
