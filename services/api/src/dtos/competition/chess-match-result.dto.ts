import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsBoolean, IsEnum, IsNumber, IsOptional, IsString, IsUUID } from 'class-validator';

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
  @ApiProperty()
  @IsUUID(4, { message: 'error.IsUUID: id should be a valid UUID' })
  id: string;

  @ApiProperty({ type: 'array', items: { type: 'string' } })
  @IsArray({ message: 'error.IsArray: players should be an array' })
  @IsString({
    each: true,
    message: 'error.IsString: players should be an array of strings',
  })
  players: string[];

  @ApiProperty({ type: 'array', items: { type: 'string' } })
  @IsArray({ message: 'error.IsArray: moves should be an array' })
  @IsString({
    each: true,
    message: 'error.IsString: moves should be an array of strings',
  })
  moves: string[];

  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: winner should be a string' })
  winner: string;

  @ApiProperty()
  @IsOptional()
  @IsBoolean({ message: 'error.IsBoolean: draw should be a boolean' })
  draw: boolean;

  @ApiProperty({ enum: ChessGameResult })
  @IsEnum(ChessGameResult, {
    message: 'error.IsEnum: result should be a valid ChessGameResult',
  })
  result: ChessGameResult;

  @ApiProperty({ enum: ChessGameResultReason })
  @IsEnum(ChessGameResultReason, {
    message: 'error.IsEnum: reason should be a valid ChessGameResultReason',
  })
  reason: ChessGameResultReason;

  @ApiProperty({ type: 'array', items: { type: 'number' } })
  @IsArray({ message: 'error.IsArray: cpuTime should be an array' })
  @IsNumber(
    {},
    {
      each: true,
      message: 'error.IsNumber: cpuTime should be an array of numbers',
    },
  )
  cpuTime: number[];

  @ApiProperty()
  @IsString({ message: 'error.IsString: initialFen should be a string' })
  finalFen: string;

  @ApiProperty({ type: 'array', items: { type: 'number' } })
  @IsArray({ message: 'error.IsArray: eloChange should be an array' })
  @IsNumber(
    {},
    {
      each: true,
      message: 'error.IsNumber: eloChange should be a number',
    },
  )
  eloChange: number[];

  @ApiProperty({ type: 'array', items: { type: 'number' } })
  @IsArray({ message: 'error.IsArray: elo should be an array' })
  @IsNumber(
    {},
    {
      each: true,
      message: 'error.IsNumber: elo should be an array of numbers',
    },
  )
  elo: number[];

  @ApiProperty()
  createdAt: Date;
}
