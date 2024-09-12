import { EntityBase } from '../../common/entities/entity.base';
import { GameVersionEntity } from './game-version.entity';
import { Column, Entity, ManyToOne } from 'typeorm';
import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity('game_feedback_response')
export class GameFeedbackResponseEntity extends EntityBase {
  // relationship to the game version
  @ManyToOne(() => GameVersionEntity, (version) => version.responses)
  @ApiProperty({ type: () => GameVersionEntity })
  @ValidateNested()
  @Type(() => GameVersionEntity)
  version: GameVersionEntity;

  // relationship to the user
  @ManyToOne(() => UserEntity)
  @ApiProperty({ type: () => UserEntity })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  // feedback responses
  @ApiProperty()
  @IsArray()
  @Column({ type: 'jsonb', nullable: false })
  responses: [string | string[] | number];

  constructor(partial: Partial<GameFeedbackResponseEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
