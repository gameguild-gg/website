import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Column, Entity, OneToMany } from 'typeorm';
import { GameDto } from '../dtos/game.dto';
import { PermissionsDto } from '../../auth/dtos/with-roles.dto';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { GameVersionDto } from '../dtos/game-version.dto';
import { GameVersionEntity } from './game-version.entity';

@Entity('game')
export class GameEntity extends ContentBase implements GameDto {
  @ApiProperty({ type: () => PermissionsDto })
  @ValidateNested({ message: 'roles must be an instance of PermissionsDto' })
  @Type(() => PermissionsDto)
  @Column('jsonb', { nullable: true, select: false })
  roles: PermissionsDto;

  @ApiProperty({ type: () => GameVersionDto, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => GameVersionDto)
  // relation to GameVersionEntity
  @OneToMany(() => GameVersionEntity, (version) => version.game)
  versions: GameVersionEntity[];
}
