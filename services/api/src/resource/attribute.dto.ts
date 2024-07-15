import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString } from 'class-validator';
import { EntityDto } from '../dtos/entity.dto';
import { UserDto } from '../dtos/user/user.dto';
import { ResourceDto } from './resource.dto';

export class AttributeDto extends EntityDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  attribute: string;

  @ApiProperty({ type: UserDto })
  user: UserDto;

  @ApiProperty({ type: ResourceDto })
  resource: ResourceDto;
}
