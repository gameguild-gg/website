import { Entity, JoinColumn, ManyToOne } from 'typeorm';
import { EntityBase } from '@/common/entities/entity.base';
import { ResourceMetadataEntity } from '@/common/entities/resource-metadata.entity';
import { UserEntity } from '@/user/entities/user.entity';
import { RoleEntity } from '@/common/entities/role.entity';

@Entity('resource_permissions')
export class ResourcePermissionEntity extends EntityBase {
  @ManyToOne(() => UserEntity, { nullable: false, onDelete: 'CASCADE', eager: true })
  @JoinColumn({ name: 'user_id' })
  public readonly user: UserEntity;

  @ManyToOne(() => ResourceMetadataEntity, { nullable: false, onDelete: 'CASCADE', eager: true })
  @JoinColumn({ name: 'resource_id' })
  public readonly resource: ResourceMetadataEntity;

  @ManyToOne(() => RoleEntity, { nullable: false, eager: true })
  @JoinColumn({ name: 'role_id' })
  public readonly role: RoleEntity;
}
