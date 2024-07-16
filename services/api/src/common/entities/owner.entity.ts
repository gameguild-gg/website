import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';
import { ManyToOne } from 'typeorm';
import { OwnerDto } from '../dtos/owner.dto';
import { EntityBase } from './entity.base';

/**
 * Owner Entity
 * @description Entity that contains the owner of the entity, it is used to add the owner to the entity, do not use it directly, only extend it or implement it if you need it
 */
export class OwnerEntity extends EntityBase implements OwnerDto {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { eager: true })
  owner: UserEntity;
}
