import { ApiProperty } from '@nestjs/swagger';
import { GameVersionDto } from './game-version.dto';

export class GameVersionCreateRequestDto {
  @ApiProperty({ description: 'The id of the game this version belongs to' })
  id: string;

  @ApiProperty()
  version: GameVersionDto;
}
