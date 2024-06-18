import { ApiProperty } from '@nestjs/swagger';
import { EntityDto } from "../entity.dto";

export class MatchSearchResponseDto extends EntityDto {
  @ApiProperty()
  winner: string;
  @ApiProperty()
  lastState: string;
  @ApiProperty({ type: [String] })
  players: string[];
}
