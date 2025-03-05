import { Column, Entity, OneToMany, Unique } from 'typeorm';

import { EntityBase } from '@/common/entities/entity.base';
import { ResourcePermissionEntity } from '@/common/entities/resource-permission.entity';

@Entity('resource_metadata')
@Unique(['resourceId', 'resourceType'])
export class ResourceMetadataEntity extends EntityBase {
  @Column({ type: 'uuid' })
  public readonly resourceId: string;

  @Column({ type: 'varchar', length: 100 })
  public readonly resourceType: string;

  @OneToMany(() => ResourcePermissionEntity, (permission) => permission.resource, {
    cascade: true,
    onDelete: 'CASCADE',
  })
  public readonly permissions: ResourcePermissionEntity[];
}
