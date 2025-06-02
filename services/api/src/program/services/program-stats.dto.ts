import { IsNumber, Min } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class ProgramStats {
  @ApiProperty({ description: 'Total number of enrollments' })
  @IsNumber()
  @Min(0)
  total_enrollments: number;

  @ApiProperty({ description: 'Completion rate as percentage (0-100)' })
  @IsNumber()
  @Min(0)
  completion_rate: number;

  @ApiProperty({ description: 'Average rating (1-5 scale)' })
  @IsNumber()
  @Min(0)
  average_rating: number;

  @ApiProperty({ description: 'Total number of ratings' })
  @IsNumber()
  @Min(0)
  total_ratings: number;

  @ApiProperty({ description: 'Number of currently active participants' })
  @IsNumber()
  @Min(0)
  active_participants: number;
}
