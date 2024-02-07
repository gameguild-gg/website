import { ApiProperty } from '@nestjs/swagger';

export enum ChessGameResultReason {
  CHECKMATE = 'CHECKMATE',
  STALEMATE = 'STALEMATE',
  INSUFFICIENT_MATERIAL = 'INSUFFICIENT_MATERIAL',
  FIFTY_MOVE_RULE = 'FIFTY_MOVE_RULE',
  THREEFOLD_REPETITION = 'THREEFOLD_REPETITION',
  INVALID_MOVE = 'INVALID_MOVE',
  NONE = 'NONE',
}

export enum ChessGameResult {
  GAME_OVER = 'GAME_OVER',
  DRAW = 'DRAW',
  NONE = 'NONE',
}

export class ChessMatchResultDto {
  @ApiProperty({ type: 'array', items: { type: 'string' } })
  players: string[];
  @ApiProperty({ type: 'array', items: { type: 'string' } })
  moves: string[];
  @ApiProperty()
  winner: string;
  @ApiProperty()
  draw: boolean;
  @ApiProperty({ enum: ChessGameResult })
  result: ChessGameResult;
  @ApiProperty({ enum: ChessGameResultReason })
  reason: ChessGameResultReason;
  @ApiProperty({ type: 'array', items: { type: 'number' } })
  cpuTime: number[];
  @ApiProperty()
  finalFen: string;
  @ApiProperty({ type: 'array', items: { type: 'number' } })
  eloChange: number[];
  @ApiProperty({ type: 'array', items: { type: 'number' } })
  elo: number[];
  @ApiProperty()
  createdAt: Date;
}
