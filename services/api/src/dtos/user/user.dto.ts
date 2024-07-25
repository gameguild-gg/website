import { ApiProperty } from '@nestjs/swagger';
import { Exclude, Type } from 'class-transformer';
import { EntityDto } from '../entity.dto';
import { UserProfileDto } from '../../user/modules/user-profile/dtos/user-profile.dto';
import { ValidateNested } from 'class-validator';

export class UserDto extends EntityDto {
  // Local Sign-in
  @ApiProperty()
  username: string;

  @ApiProperty()
  email: string;

  @ApiProperty()
  emailVerified: boolean;

  @ApiProperty()
  @Exclude()
  passwordHash: string;

  @ApiProperty()
  @Exclude()
  passwordSalt: string;

  // Social Sign-in
  @ApiProperty()
  facebookId: string;

  @ApiProperty()
  googleId: string;

  @ApiProperty()
  githubId: string;

  @ApiProperty()
  appleId: string;

  @ApiProperty()
  linkedinId: string;

  @ApiProperty()
  twitterId: string;

  // Web3 Sign-in
  @ApiProperty()
  walletAddress: string;

  // chess elo rank
  @ApiProperty()
  elo: number;

  @ApiProperty({ type: UserProfileDto })
  @ValidateNested()
  @Type(() => UserProfileDto)
  profile: UserProfileDto;

  constructor(partial: Partial<UserDto>) {
    super();
    Object.assign(this, partial);
  }
}
