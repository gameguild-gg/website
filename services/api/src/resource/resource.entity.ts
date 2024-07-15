import { EntityBase } from '../common/entities/entity.base';
import { ResourceDto } from './resource.dto';
import { AttributeEntity } from './attribute.entity';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, OneToMany } from 'typeorm';

export class ResourceEntity extends EntityBase implements ResourceDto {
  @ApiProperty({ type: AttributeEntity, isArray: true })
  @OneToMany(() => AttributeEntity, (attr) => attr.resource)
  userAttributes: AttributeEntity[];
}
