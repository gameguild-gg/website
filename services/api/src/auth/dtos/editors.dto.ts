import { UserDto } from '../../dtos/user/user.dto';
import { ApiProperty } from '@nestjs/swagger';
import { OwnerDto } from './owner.dto';

export class EditorsDto extends OwnerDto {
  @ApiProperty({ type: () => UserDto, isArray: true })
  editors: UserDto[];
}
