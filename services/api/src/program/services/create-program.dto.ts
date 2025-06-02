import { IsString, IsOptional, IsJSON, IsArray } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class CreateProgramDto {
  @ApiProperty({ description: 'URL-friendly identifier for the program' })
  @IsString()
  slug: string;

  @ApiProperty({ description: 'Brief description of the program for listings and previews' })
  @IsString()
  summary: string;

  @ApiProperty({ description: 'Main program content in JSON format, includes description, objectives, etc.' })
  @IsJSON()
  body: object;

  @ApiProperty({ description: 'Array of allowed email domains, null if not tenancy-fenced', type: [String], required: false })
  @IsOptional()
  @IsArray()
  tenancyDomains?: string[];

  @ApiProperty({ description: 'Program type specific data, late penalty etc', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;
}
