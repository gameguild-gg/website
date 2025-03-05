import { Column, Index, OneToOne } from 'typeorm';

import { ResourceDto } from '@/common/dtos/resource-dto';
import { VisibilityStatus } from '@/common/dtos/visibility-status.enum';
import { EntityBase } from '@/common/entities/entity.base';
import { ResourceMetadataEntity } from '@/common/entities/resource-metadata.entity';
import { UserEntity } from '@/user/entities/user.entity';

export abstract class ResourceBase extends EntityBase implements ResourceDto {
  @OneToOne(() => ResourceMetadataEntity, { cascade: true, onDelete: 'CASCADE' })
  public readonly metadata: ResourceMetadataEntity;

  @Column({ nullable: false })
  @Index({ unique: false })
  @OneToOne(() => UserEntity)
  public readonly owner: UserEntity;

  @Column({ type: 'enum', nullable: false, default: VisibilityStatus.PRIVATE, enum: VisibilityStatus })
  @Index({ unique: false })
  public readonly visibility: VisibilityStatus;
}
