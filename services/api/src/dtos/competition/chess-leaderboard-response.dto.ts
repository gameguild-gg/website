import { ApiProperty } from '@nestjs/swagger';

export class ChessLeaderboardResponseEntryDto {
  @ApiProperty()
  id: string;

  @ApiProperty()
  username: string;

  @ApiProperty()
  elo: number;
}

export type ChessLeaderboardResponseDto = ChessLeaderboardResponseEntryDto[];
