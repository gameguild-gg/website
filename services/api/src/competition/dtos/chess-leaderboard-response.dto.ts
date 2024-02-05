import { ApiProperty } from "@nestjs/swagger";

export class ChessLeaderboardResponseEntryDto {
  @ApiProperty()
  username: string;
  @ApiProperty()
  elo: number;
}

export type ChessLeaderboardResponseDto = ChessLeaderboardResponseEntryDto[];