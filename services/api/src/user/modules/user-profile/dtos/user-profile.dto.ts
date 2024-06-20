import { EntityDto } from '../../../../dtos/entity.dto';
import { ApiProperty } from '@nestjs/swagger';

export class UserProfileDto extends EntityDto {
  @ApiProperty()
  bio?: string;
  @ApiProperty()
  name?: string;
  @ApiProperty()
  givenName?: string;
  @ApiProperty()
  familyName?: string;
  @ApiProperty()
  picture?: string;
}
