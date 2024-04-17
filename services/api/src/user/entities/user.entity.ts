import { ApiProperty } from '@nestjs/swagger';
import { Exclude } from 'class-transformer';
import {
  Column,
  Entity,
  Index,
  JoinColumn, JoinTable, ManyToMany,
  OneToMany,
  OneToOne
} from "typeorm";
import { EntityBase } from '../../common/entities/entity.base';
import { UserProfileEntity } from '../modules/user-profile/entities/user-profile.entity';
import { CompetitionSubmissionEntity } from '../../competition/entities/competition.submission.entity';
import { UserDto } from "../../dtos/user/user.dto";
import { PostEntity } from "../../cms/entities/post.entity";
import { CourseEntity } from "../../cms/entities/course.entity";

// todo: move to user-profile lots of fields from here

@Entity({ name: 'user' })
export class UserEntity extends EntityBase implements UserDto {
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
  @OneToOne(() => UserProfileEntity, (profile) => profile.user, {
    cascade: true,
    onDelete: 'CASCADE',
    onUpdate: 'CASCADE',
  })
  @JoinColumn()
  profile: UserProfileEntity;

  @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  competitionSubmissions: CompetitionSubmissionEntity[];

  // chess elo rank
  @Column({ type: 'float', default: 400 })
  @ApiProperty()
  elo: number;
  
  // relation to posts many to many
  @ManyToMany(() => PostEntity, (post) => post.owners)
  @JoinTable()
  posts: PostEntity[];
  
  // relation to courses created
  @OneToMany(() => CourseEntity, (course) => course.author)
  courses: CourseEntity[];
  
  // relation to courses enrolled
  @ManyToMany(() => CourseEntity, (course) => course.author)
  @JoinTable()
  enrolledCourses: CourseEntity[];
}
