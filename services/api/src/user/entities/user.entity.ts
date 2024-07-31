import { ApiProperty } from '@nestjs/swagger';
import { Exclude, Type } from 'class-transformer';
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
import {
  IsArray,
  IsBoolean,
  IsOptional,
  ValidateNested,
} from 'class-validator';

// todo: move to user-profile lots of fields from here

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
  // Local Sign-in
  @ApiProperty()
  @Column({
    unique: true,
    nullable: true,
    default: null,
    type: 'varchar',
    length: 32,
  })
  @Index({ unique: true })
  @IsOptional()
  @IsUsername()
  username: string;

  @Column({
    unique: true,
    nullable: true,
    default: null,
    type: 'varchar',
    length: 254,
  })
  @ApiProperty()
  @Index({ unique: true })
  @IsOptional()
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
  @ValidateNested()
  @Type(() => UserProfileEntity)
  profile: UserProfileEntity;

  @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  @ApiProperty({ type: CompetitionSubmissionEntity, isArray: true })
  @IsArray({
    message: 'error.IsArray: competitionSubmissions should be an array',
  })
  @ValidateNested({ each: true })
  @Type(() => CompetitionSubmissionEntity)
  competitionSubmissions: CompetitionSubmissionEntity[];

  // chess elo rank
  @Column({ type: 'float', default: 400 })
  @ApiProperty()
  elo: number;

  // relation to posts many to many
  @ManyToMany(() => PostEntity, (post) => post.owners)
  @JoinTable()
  @ApiProperty({ type: PostEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: posts should be an array' })
  @ValidateNested({ each: true })
  @Type(() => PostEntity)
  posts: PostEntity[];

  // relation to courses
  @OneToMany(() => CourseEntity, (course) => course.author)
  @ApiProperty({ type: CourseEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: courses should be an array' })
  @ValidateNested({ each: true })
  @Type(() => CourseEntity)
  courses: CourseEntity[];

  constructor(partial: Partial<UserEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
