import { IsString, IsOptional, IsNumber } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';

export class EnrollmentFilters {
  @ApiProperty({ description: 'Filter by program ID', required: false })
  @IsOptional()
  @IsString()
  programId?: string;

  @ApiProperty({ description: 'Filter by user ID', required: false })
  @IsOptional()
  @IsString()
  userId?: string;

  @ApiProperty({ description: 'Search term for program names or user details', required: false })
  @IsOptional()
  @IsString()
  search?: string;

  @ApiProperty({ description: 'Maximum number of results to return', required: false })
  @IsOptional()
  @IsNumber()
  @Type(() => Number)
  limit?: number;

  @ApiProperty({ description: 'Number of results to skip for pagination', required: false })
  @IsOptional()
  @IsNumber()
  @Type(() => Number)
  offset?: number;
}
