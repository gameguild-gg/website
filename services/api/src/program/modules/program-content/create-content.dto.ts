import { IsString, IsOptional, IsBoolean, IsNumber, IsArray, IsEnum, IsJSON, IsDate, Min } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ProgramContentType, GradingMethod } from '../../entities/enums';

export class CreateContentDto {
  @ApiProperty({ description: 'ID of the program this content belongs to' })
  @IsString()
  programId: string;

  @ApiProperty({ description: 'ID of the parent content for hierarchical structure', required: false })
  @IsOptional()
  @IsString()
  parentId?: string;

  @ApiProperty({ enum: ProgramContentType, description: 'Type of content' })
  @IsEnum(ProgramContentType)
  type: ProgramContentType;

  @ApiProperty({ description: 'Title/name of the content' })
  @IsString()
  title: string;

  @ApiProperty({ description: 'Brief description of the content', required: false })
  @IsOptional()
  @IsString()
  summary?: string;

  @ApiProperty({ description: 'Position value for sorting content', required: false })
  @IsOptional()
  @IsNumber()
  order?: number;

  @ApiProperty({ description: 'Main content in structured JSON format' })
  @IsJSON()
  body: object;

  @ApiProperty({ description: 'Flag to indicate if this content can be accessed without purchasing the program', required: false })
  @IsOptional()
  @IsBoolean()
  previewable?: boolean;

  @ApiProperty({ description: 'Deadline for completion', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  dueDate?: Date;

  @ApiProperty({ description: 'When this content becomes available to users', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  availableFrom?: Date;

  @ApiProperty({ description: 'When this content becomes unavailable', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  availableTo?: Date;

  @ApiProperty({ enum: GradingMethod, description: 'Method used to grade this content', required: false })
  @IsOptional()
  @IsEnum(GradingMethod)
  gradingMethod?: GradingMethod;

  @ApiProperty({ description: 'Time limit in minutes for timed activities', required: false })
  @IsOptional()
  @IsNumber()
  @Min(1)
  durationMinutes?: number;

  @ApiProperty({ description: 'Whether text response is accepted for submission', required: false })
  @IsOptional()
  @IsBoolean()
  textResponse?: boolean;

  @ApiProperty({ description: 'Whether URL submission is accepted', required: false })
  @IsOptional()
  @IsBoolean()
  urlResponse?: boolean;

  @ApiProperty({ description: 'Allowed file extensions for uploads', type: [String], required: false })
  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  fileResponseExtensions?: string[];

  @ApiProperty({ description: 'Detailed rubric configuration for consistent grading', required: false })
  @IsOptional()
  @IsJSON()
  gradingRubric?: object;

  @ApiProperty({ description: 'Flexible storage for content-specific settings', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @ApiProperty({ description: 'Points value for this content', required: false })
  @IsOptional()
  @IsNumber()
  @Min(0)
  points_value?: number;
}
