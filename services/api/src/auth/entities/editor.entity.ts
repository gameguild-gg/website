import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';
import { ManyToOne } from 'typeorm';
import { EditorsDto } from '../dtos/editors.dto';
import { OwnerEntity } from './owner.entity';

/**
 * Editor Entity
 * @description Entity that contains the editors of the entity, it is used to add the editors to the entity, do not use it directly! Only extend it or implement it if you need it
 */
export class EditorsEntity extends OwnerEntity implements EditorsDto {
  @ApiProperty({ type: () => EditorsEntity, isArray: true })
  @ManyToOne(() => EditorsEntity, { lazy: true })
  editors: UserEntity[];
}
