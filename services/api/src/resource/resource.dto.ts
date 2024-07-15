import { EntityDto } from '../dtos/entity.dto';
import { ApiProperty } from '@nestjs/swagger';
import { AttributeDto } from './attribute.dto';

export class ResourceDto extends EntityDto {
  @ApiProperty({ type: AttributeDto, isArray: true })
  userAttributes: AttributeDto[];
}
