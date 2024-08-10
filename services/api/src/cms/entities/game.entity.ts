import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, OneToMany } from 'typeorm';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { GameVersionEntity } from './game-version.entity';

@Entity('game')
export class GameEntity extends ContentBase {
  @ApiProperty({ type: GameVersionEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => GameVersionEntity)
  // relation to GameVersionEntity
  @OneToMany(() => GameVersionEntity, (version) => version.game)
  versions: GameVersionEntity[];

  constructor(partial?: Partial<GameEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
