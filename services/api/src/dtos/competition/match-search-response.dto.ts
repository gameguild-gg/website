import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';

export class MatchSearchResponseDto extends EntityBase {
  @ApiProperty()
  winner: string;
  @ApiProperty()
  lastState: string;
  @ApiProperty({ type: [String] })
  players: string[];

  constructor(partial: Partial<MatchSearchResponseDto>) {
    super(partial);
    Object.assign(this, partial);
  }
}
