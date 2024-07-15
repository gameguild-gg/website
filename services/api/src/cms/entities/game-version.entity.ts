import { EntityDto } from '../../dtos/entity.dto';
import { Column, Entity, ManyToOne } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { GameEntity } from './game.entity';
import { EntityBase } from '../../common/entities/entity.base';

@Entity('game_version')
export class GameVersionEntity extends EntityBase {
  @ApiProperty()
  @Column()
  version: string;

  @ApiProperty()
  @Column()
  archive_url: string;

  // notes / testing plan
  @ApiProperty()
  @Column()
  notes_url: string;

  // feedback form
  @ApiProperty()
  @Column()
  feedback_form: string;

  // deadline
  @ApiProperty()
  @Column({ type: 'timestamptz' })
  deadline: Date;

  @ManyToOne(() => GameEntity, (game) => game.versions)
  game: GameEntity;
}
