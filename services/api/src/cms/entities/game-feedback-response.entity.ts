import { EntityBase } from '../../common/entities/entity.base';
import { GameVersionEntity } from './game-version.entity';
import { Entity, ManyToOne } from 'typeorm';
import { UserEntity } from '../../user/entities';
import { GameFeedbackResponseDto } from '../dtos/game-feedback-response.dto';
import { ApiProperty } from '@nestjs/swagger';

@Entity('game_feedback_response')
export class GameFeedbackResponseEntity
  extends EntityBase
  implements GameFeedbackResponseDto
{
  // relationship to the game version
  @ManyToOne(() => GameVersionEntity, (version) => version.responses)
  @ApiProperty({ type: () => GameVersionEntity })
  version: GameVersionEntity;

  // relationship to the user
  @ManyToOne(() => UserEntity)
  @ApiProperty({ type: () => UserEntity })
  user: UserEntity;

  // add other fields here, for now if the data is here, the user has already submitted the feedback
}
