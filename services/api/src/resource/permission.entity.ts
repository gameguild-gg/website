import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString } from 'class-validator';
import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { PermissionDto } from './permission.dto';
import { UserEntity } from '../user/entities';
import { EntityBase } from '../common/entities/entity.base';
import { ResourceEntity } from './resource.entity';

@Entity({ name: 'permissions' })
export class PermissionEntity extends EntityBase implements PermissionDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @Column({ nullable: false })
  @Index({ unique: false })
  attribute: string;

  @ApiProperty({ type: UserEntity })
  @ManyToOne(() => UserEntity)
  user: UserEntity;

  // todo: @matheus, this is not working as I imagined.
  @ApiProperty({ type: ResourceEntity })
  @ManyToOne(() => ResourceEntity, (resource) => resource.permissions)
  resource: ResourceEntity;
}
