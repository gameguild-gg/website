import { IsString, IsOptional, IsArray, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class ProgramFilters {
  @ApiProperty({ description: 'Filter by slug pattern', required: false })
  @IsOptional()
  @IsString()
  slug?: string;

  @ApiProperty({ description: 'Search term for summary', required: false })
  @IsOptional()
  @IsString()
  search?: string;

  @ApiProperty({ description: 'Filter by tenancy domains', type: [String], required: false })
  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tenancyDomains?: string[];

  @ApiProperty({ description: 'Filter by metadata properties', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;
}
