import { ApiProperty } from '@nestjs/swagger';

export class ChessAgentResponseEntryDto {
  @ApiProperty({ description: 'The unique identifier of the user' })
  id: string;
  
  @ApiProperty({ description: 'The username of the user who owns the chess agent' })
  username: string;
  
  @ApiProperty({ description: 'The ELO rating of the chess agent' })
  elo: number;
}

export type ChessAgentsResponseDto = ChessAgentResponseEntryDto[]; 