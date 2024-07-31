import { ApiProperty } from '@nestjs/swagger';
import { GameVersionEntity } from '../entities/game-version.entity';

export class GameVersionCreateRequestDto {
  @ApiProperty({ description: 'The id of the game this version belongs to' })
  gameId: string;

  @ApiProperty({ type: () => GameVersionEntity })
  version: GameVersionEntity;
}
