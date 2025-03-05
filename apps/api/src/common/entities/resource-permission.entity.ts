import { Column, Entity, JoinColumn, ManyToOne } from 'typeorm';

import { PermissionType } from '@/common/dtos/permission-type.enum';
import { EntityBase } from '@/common/entities/entity.base';
import { ResourceMetadataEntity } from '@/common/entities/resource-metadata.entity';
import { UserEntity } from '@/user/entities/user.entity';

@Entity('resource_permissions')
export class ResourcePermissionEntity extends EntityBase {
  @ManyToOne(() => UserEntity, { nullable: false, onDelete: 'CASCADE', eager: true })
  @JoinColumn({ name: 'user_id' })
  public readonly user: UserEntity;

  @ManyToOne(() => ResourceMetadataEntity, { nullable: false, onDelete: 'CASCADE', eager: true })
  @JoinColumn({ name: 'resource_id' })
  public readonly resource: ResourceMetadataEntity;

  @Column({ type: 'enum', enum: PermissionType })
  public readonly permission: PermissionType;
}
