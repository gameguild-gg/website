import { ApiProperty } from '@nestjs/swagger';
import { Exclude, Type } from 'class-transformer';
import { Column, Entity, Index, JoinColumn, JoinTable, ManyToMany, OneToMany, OneToOne } from 'typeorm';
import { ObjectType, Field } from '@nestjs/graphql';
import { EntityBase } from '../../common/entities/entity.base';
import { UserProfileEntity } from '../modules/user-profile/entities/user-profile.entity';
import { CompetitionSubmissionEntity } from '../../competition/entities/competition.submission.entity';
import { PostEntity } from '../../cms/entities/post.entity';
import { IsEmail, IsUsername } from '../../common/decorators/validator.decorator';
import { IsArray, IsBoolean, IsOptional, ValidateNested } from 'class-validator';

// todo: move to user-profile lots of fields from here

@ObjectType()
@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
  // Local Sign-in
  @Field({ nullable: true })
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

  @Field({ nullable: true })
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

  @Field()
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
  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  facebookId: string;

  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  googleId: string;

  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  githubId: string;

  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  appleId: string;

  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  linkedinId: string;

  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  twitterId: string;

  // Web3 Sign-in
  @Field({ nullable: true })
  @Column({ nullable: true, unique: true, default: null })
  @ApiProperty()
  @Index({ unique: true })
  walletAddress: string;

  // Profile
  @Field(() => UserProfileEntity, { nullable: true })
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

  @Field(() => [CompetitionSubmissionEntity])
  @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  @ApiProperty({ type: CompetitionSubmissionEntity, isArray: true })
  @IsArray({
    message: 'error.IsArray: competitionSubmissions should be an array',
  })
  @ValidateNested({ each: true })
  @Type(() => CompetitionSubmissionEntity)
  competitionSubmissions: CompetitionSubmissionEntity[];

  // chess elo rank
  @Field()
  @Column({ type: 'float', default: 400 })
  @ApiProperty()
  elo: number;
}
