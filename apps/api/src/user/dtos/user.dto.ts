import { ApiProperty, ApiSchema } from '@nestjs/swagger';
import { Exclude, Type } from 'class-transformer';
import { IsBoolean, IsEmail, IsOptional, ValidateNested } from 'class-validator';

import { EntityDto } from '@/common/dtos/entity.dto';
import { IsUsername } from '@/legacy/common/decorators/validator.decorator';
import { UserProfileDto } from '@/user/modules/user-profile/dtos/user-profile.dto';

@ApiSchema({ name: 'User' })
export class UserDto extends EntityDto {
  // Local Sign-in

  @ApiProperty()
  @IsUsername()
  @IsOptional()
  public readonly username: string;

  @ApiProperty()
  @IsEmail()
  @IsOptional()
  public readonly email: string;

  @ApiProperty()
  @IsBoolean()
  public readonly emailVerified: boolean;

  @Exclude()
  public readonly passwordHash: string;

  @Exclude()
  public readonly passwordSalt: string;

  // Social Sign-in

  @ApiProperty()
  @IsOptional()
  public readonly appleId: string;

  @ApiProperty()
  @IsOptional()
  public readonly facebookId: string;

  @ApiProperty()
  @IsOptional()
  public readonly githubId: string;

  @ApiProperty()
  @IsOptional()
  public readonly googleId: string;

  @ApiProperty()
  @IsOptional()
  public readonly linkedinId: string;

  @ApiProperty()
  @IsOptional()
  public readonly twitterId: string;

  // Web3 Sign-in

  @ApiProperty()
  @IsOptional()
  public readonly walletAddress: string;

  // Profile

  @ApiProperty({ type: UserProfileDto })
  @Type(() => UserProfileDto)
  @IsOptional()
  @ValidateNested()
  public readonly profile: UserProfileDto;
}
