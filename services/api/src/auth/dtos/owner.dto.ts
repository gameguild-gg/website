import { UserDto } from '../../dtos/user/user.dto';
import { ApiProperty } from '@nestjs/swagger';
import { EntityDto } from '../../dtos/entity.dto';

export class OwnerDto extends EntityDto {
  @ApiProperty({ type: () => UserDto })
  owner: UserDto | string;
}
