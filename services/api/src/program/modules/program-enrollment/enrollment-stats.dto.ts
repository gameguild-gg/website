import { IsNumber, Min, Max } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class EnrollmentStats {
  @ApiProperty({ description: 'Total number of enrollments' })
  @IsNumber()
  @Min(0)
  total_enrollments: number;

  @ApiProperty({ description: 'Number of active enrollments' })
  @IsNumber()
  @Min(0)
  active_enrollments: number;

  @ApiProperty({ description: 'Number of completed enrollments' })
  @IsNumber()
  @Min(0)
  completed: number;

  @ApiProperty({ description: 'Number of in-progress enrollments' })
  @IsNumber()
  @Min(0)
  in_progress: number;

  @ApiProperty({ description: 'Number of not-started enrollments' })
  @IsNumber()
  @Min(0)
  not_started: number;

  @ApiProperty({ description: 'Completion rate as percentage (0-100)' })
  @IsNumber()
  @Min(0)
  @Max(100)
  completion_rate: number;

  @ApiProperty({ description: 'Average progress percentage (0-100)' })
  @IsNumber()
  @Min(0)
  @Max(100)
  average_progress: number;

  @ApiProperty({ description: 'Dropout rate as percentage (0-100)' })
  @IsNumber()
  @Min(0)
  @Max(100)
  dropout_rate: number;
}
