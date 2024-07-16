import { Column, Entity, ManyToOne, OneToMany } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { GameEntity } from './game.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { GameFeedbackResponseEntity } from './game-feedback-response.entity';

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
  feedback_deadline: Date;

  @ManyToOne(() => GameEntity, (game) => game.versions)
  @ApiProperty({ type: () => GameEntity })
  game: GameEntity;

  // relation to feedback responses
  @ApiProperty({ type: () => GameFeedbackResponseEntity, isArray: true })
  @OneToMany(() => GameFeedbackResponseEntity, (response) => response.version)
  responses: GameFeedbackResponseEntity[];
}
