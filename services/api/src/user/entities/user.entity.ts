import { ApiProperty } from '@nestjs/swagger';
import { Exclude } from 'class-transformer';
import {Column, Entity, Index, OneToMany, OneToOne} from 'typeorm';
import { EntityBase } from '../../common/entities/entity.base';
import { UserProfileEntity } from '../modules/user-profile/entities/user-profile.entity';
import {CompetitionSubmissionEntity} from "../../competition/entities/competition.submission.entity";

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
  // Local Sign-in
  @ApiProperty()
  @Column({ unique: true, nullable: true, default: null })
  @Index()
  username: string;

  @Column({ unique: true, nullable: false, default: null })
  @ApiProperty()
  @Index()
  email: string;

  @Column({ nullable: false, default: false })
  @ApiProperty()
  emailVerified: boolean;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Exclude()
  passwordHash: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Exclude()
  passwordSalt: string;

  // Social Sign-in
  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  facebookId: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  googleId: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  githubId: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  appleId: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  linkedinId: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  twitterId: string;

  // Web3 Sign-in
  @Column({ nullable: true, default: null })
  @ApiProperty()
  @Index()
  walletAddress: string;

  // Profile
  @OneToOne(() => UserProfileEntity, (profile) => profile.user)
  profile: UserProfileEntity;
  
  @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  competitionSubmissions: CompetitionSubmissionEntity[];
  
}
