// import { ApiProperty } from '@nestjs/swagger';
// import { Exclude, Type } from 'class-transformer';
// import { Column, Entity, Index, JoinColumn, OneToMany, OneToOne } from 'typeorm';
// import { EntityBase } from '../../common/entities/entity.base';
// import { UserProfileEntity } from '../modules/user-profile/entities/user-profile.entity';
// import { CompetitionSubmissionEntity } from '../../competition/entities/competition.submission.entity';
// import { IsEmail, IsUsername } from '../../common/decorators/validator.decorator';
// import { IsArray, IsBoolean, IsOptional, ValidateNested } from 'class-validator';
//
// // todo: move to user-profile lots of fields from here
//
import { Column, Entity, Index, JoinColumn, OneToOne } from 'typeorm';
import { EntityBase } from '@/common/entities/entity.base';
import { UserDto } from '@/user/dtos/user.dto';
import { UserProfileEntity } from '@/user/modules/user-profile/entities/user-profile.entity';

@Entity({ name: 'user' })
export class UserEntity extends EntityBase implements UserDto {
  // Local Sign-in

  @Column({ type: 'varchar', length: 32, nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly username: string;

  @Column({ type: 'varchar', length: 254, nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly email: string;

  @Column({ nullable: true, default: null })
  public readonly passwordHash: string;

  @Column({ nullable: true, default: null })
  public readonly passwordSalt: string;

  @Column({ nullable: false, default: false })
  public readonly emailVerified: boolean;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly appleId: string;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly facebookId: string;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly githubId: string;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly googleId: string;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly linkedinId: string;

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly twitterId: string;

  // Web3 Sign-in

  @Column({ nullable: true, unique: true, default: null })
  @Index({ unique: true })
  public readonly walletAddress: string;

  // Profile

  @OneToOne(() => UserProfileEntity, (profile) => profile.user, {
    cascade: true,
    onDelete: 'CASCADE',
    onUpdate: 'CASCADE',
  })
  @JoinColumn()
  public readonly profile: UserProfileEntity;

  //   @OneToMany(() => CompetitionSubmissionEntity, (s) => s.user)
  //   @ApiProperty({ type: CompetitionSubmissionEntity, isArray: true })
  //   @IsArray({
  //     message: 'error.IsArray: competitionSubmissions should be an array',
  //   })
  //   @ValidateNested({ each: true })
  //   @Type(() => CompetitionSubmissionEntity)
  //   competitionSubmissions: CompetitionSubmissionEntity[];
  //
  //   // chess elo rank
  //   @Column({ type: 'float', default: 400 })
  //   @ApiProperty()
  //   elo: number;
}
