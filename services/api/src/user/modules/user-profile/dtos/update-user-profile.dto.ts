// update user-profile dto is the same user-profile entity except for the id field

import { UserProfileEntity } from '../entities/user-profile.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString, IsUrl, Length, MaxLength } from 'class-validator';
import { IPickFields } from '../../../../types';

type AllowedFields = 'bio' | 'familyName' | 'givenName';

export class UpdateUserProfileDto implements IPickFields<UserProfileEntity, AllowedFields> {
  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Length(1, 256)
  bio: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Length(1, 256)
  givenName: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Length(1, 256)
  familyName: string;
}
