import { EntityBase } from '../common/entities/entity.base';
import { ResourceDto } from './resource.dto';
import { PermissionEntity } from './permission.entity';
import { ApiProperty } from '@nestjs/swagger';
import { OneToMany } from 'typeorm';

/**
 * Resource entity
 * @brief Resource entity
 * @description to be used as inherited class for any resource
 */
export class ResourceEntity extends EntityBase implements ResourceDto {
  @ApiProperty({ type: PermissionEntity, isArray: true })
  @OneToMany(() => PermissionEntity, (permission) => permission.resource)
  permissions: PermissionEntity[];
}
