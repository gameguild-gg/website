import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Column, Entity, OneToMany } from 'typeorm';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { GameVersionEntity } from './game-version.entity';
import { Permissions } from '../../auth/entities/with-roles.entity';

@Entity('game')
export class GameEntity extends ContentBase {
  @ApiProperty({ type: Permissions })
  @ValidateNested({ message: 'roles must be an instance of PermissionsDto' })
  @Type(() => Permissions)
  @Column('jsonb', { nullable: true, select: false })
  roles: Permissions;

  @ApiProperty({ type: GameVersionEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => GameVersionEntity)
  // relation to GameVersionEntity
  @OneToMany(() => GameVersionEntity, (version) => version.game)
  versions: GameVersionEntity[];
}
