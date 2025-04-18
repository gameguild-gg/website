import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';

export class MatchSearchResponseDto extends EntityBase {
  @ApiProperty()
  winner: string;
  @ApiProperty()
  lastState: string;
  @ApiProperty({ type: [String] })
  players: string[];
  @ApiProperty({ type: [Number] })
  points: number[];
  @ApiProperty({ type: [Number] })
  cpuTime: number[];
}
