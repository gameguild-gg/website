import { EntityDto } from '../dtos/entity.dto';
import { ApiProperty } from '@nestjs/swagger';
import { PermissionDto } from './permission.dto';

export class ResourceDto extends EntityDto {
  @ApiProperty({ type: PermissionDto, isArray: true })
  permissions: PermissionDto[];
}
