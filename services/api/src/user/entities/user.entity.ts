import { ApiProperty } from '@nestjs/swagger';
import { Exclude } from 'class-transformer';
import {
  Column,
  Entity,
  Index,
  JoinColumn,
  JoinTable,
  ManyToMany,
  OneToMany,
  OneToOne,
} from 'typeorm';
import { EntityBase } from '../../common/entities/entity.base';
import { UserProfileEntity } from '../modules/user-profile/entities/user-profile.entity';
import { CompetitionSubmissionEntity } from '../../competition/entities/competition.submission.entity';
import { PostEntity } from '../../cms/entities/post.entity';
import { CourseEntity } from '../../cms/entities/course.entity';
import {
  IsEmail,
  IsUsername,
} from '../../common/decorators/validator.decorator';
import { IsBoolean } from 'class-validator';

// todo: move to user-profile lots of fields from here

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
  // Local Sign-in
  @ApiProperty()
  @Column({ unique: true, nullable: true, default: null })
  @Index({ unique: true })
  @IsUsername()
  username: string;

  @Column({ unique: true, nullable: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  @IsEmail()
  email: string;

  @Column({ nullable: false, default: false })
  @ApiProperty()
  @IsBoolean()
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
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  facebookId: string;

  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  googleId: string;

  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  githubId: string;

  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  appleId: string;

  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  linkedinId: string;

  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  twitterId: string;

  // Web3 Sign-in
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  walletAddress: string;

  // Profile
  @OneToOne(() => UserProfileEntity, (profile) => profile.user, {
    cascade: true,
    onDelete: 'CASCADE',
    onUpdate: 'CASCADE',
  })
  @JoinColumn()
  @ApiProperty({ type: UserProfileEntity })
  profile: UserProfileEntity;

  @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  @ApiProperty({ type: CompetitionSubmissionEntity, isArray: true })
  competitionSubmissions: CompetitionSubmissionEntity[];

  // chess elo rank
  @Column({ type: 'float', default: 400 })
  @ApiProperty()
  elo: number;

  // relation to posts many to many
  @ManyToMany(() => PostEntity, (post) => post.owners)
  @JoinTable()
  @ApiProperty({ type: PostEntity, isArray: true })
  posts: PostEntity[];

  // relation to courses
  @OneToMany(() => CourseEntity, (course) => course.author)
  @ApiProperty({ type: CourseEntity, isArray: true })
  courses: CourseEntity[];

  constructor(partial: Partial<UserEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
