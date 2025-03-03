import { ApiProperty, ApiSchema } from '@nestjs/swagger';
import { IsOptional, IsString, MaxLength } from 'class-validator';

import { EntityDto } from '@/common/dtos/entity.dto';
import { UserDto } from '@/user/dtos/user.dto';
import { DISPLAY_NAME_MAX_LENGTH, FAMILY_NAME_MAX_LENGTH, GIVEN_NAME_MAX_LENGTH } from '@/user/modules/user-profile/user-profile.constants';

@ApiSchema({ name: 'UserProfile' })
export class UserProfileDto extends EntityDto {
  public readonly user: UserDto;

  // TODO: Move to the user dto, as it is not specific to the user profile. Only public fields should be here.
  @ApiProperty()
  @IsString({ message: 'error.IsString: given name  should be a string' })
  @MaxLength(GIVEN_NAME_MAX_LENGTH, {
    message: `error.MaxLength: given name should be at most ${GIVEN_NAME_MAX_LENGTH} characters`,
  })
  @IsOptional()
  public readonly givenName?: string;

  @ApiProperty()
  @IsString({ message: 'error.IsString: family name should be a string' })
  @MaxLength(FAMILY_NAME_MAX_LENGTH, {
    message: `error.MaxLength: family name should be at most ${FAMILY_NAME_MAX_LENGTH} characters`,
  })
  @IsOptional()
  public readonly familyName?: string;

  @ApiProperty()
  @IsString({ message: 'error.IsString: family name should be a string' })
  @MaxLength(DISPLAY_NAME_MAX_LENGTH, {
    message: `error.MaxLength: location should be at most ${DISPLAY_NAME_MAX_LENGTH} characters`,
  })
  @IsOptional()
  public readonly displayName?: string;
}
