import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, JoinTable, ManyToMany, ManyToOne, OneToMany } from 'typeorm';
import { UserEntity } from '../../user/entities';
import { GameVersionEntity } from './game-version.entity';
import { GameDto } from '../dtos/game.dto';

@Entity('game')
export class GameEntity extends ContentBase implements GameDto {
  // owners
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity)
  owner: UserEntity;

  // editors
  @ApiProperty({ type: () => UserEntity, isArray: true })
  @ManyToMany(() => UserEntity)
  @JoinTable()
  editors: UserEntity[];

  @ApiProperty({ type: () => GameVersionEntity, isArray: true })
  @OneToMany(() => GameVersionEntity, (version) => version.game)
  versions: GameVersionEntity[];
}
