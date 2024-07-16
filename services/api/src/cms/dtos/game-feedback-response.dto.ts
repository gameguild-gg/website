import { EntityDto } from '../../dtos/entity.dto';
import { ApiProperty } from '@nestjs/swagger';
import { GameVersionDto } from './game-version.dto';
import { UserDto } from '../../dtos/user/user.dto';

export class GameFeedbackResponseDto extends EntityDto {
  // relationship to the game version
  @ApiProperty({ type: () => GameVersionDto })
  version: GameVersionDto;

  // relationship to the user
  @ApiProperty({ type: () => UserDto })
  user: UserDto;

  // add other fields here, for now if the data is here, the user has already submitted the feedback
}
