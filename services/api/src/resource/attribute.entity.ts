import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString } from 'class-validator';
import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { UserEntity } from '../user/entities';
import { EntityBase } from '../common/entities/entity.base';
import { ResourceEntity } from './resource.entity';
import { AttributeDto } from './attribute.dto';

export class AttributeEntity extends EntityBase implements AttributeDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @Column({ nullable: false })
  @Index({ unique: false })
  attribute: string;

  @ApiProperty({ type: UserEntity })
  @ManyToOne(() => UserEntity)
  user: UserEntity;

  @ApiProperty({ type: ResourceEntity })
  @ManyToOne(() => ResourceEntity, (resource) => resource.userAttributes)
  resource: ResourceEntity;
}
