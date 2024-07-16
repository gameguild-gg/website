import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { UserEntity } from '../../user/entities';
import { UserDto } from '../../dtos/user/user.dto';
import { GameVersionDto } from './game-version.dto';

export class GameDto extends ContentBase {
  // owners
  @ApiProperty({ type: () => UserDto })
  owner: UserDto;

  // editors
  @ApiProperty({ type: () => UserEntity, isArray: true })
  team: UserEntity[];

  @ApiProperty({ type: () => GameVersionDto, isArray: true })
  versions: GameVersionDto[];
}
