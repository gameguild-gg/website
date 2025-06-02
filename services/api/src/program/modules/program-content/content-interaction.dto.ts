import { IsString, IsOptional, IsNumber, IsEnum, IsJSON, IsDate } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ProgressStatus } from '../../entities/enums';

export class ContentInteractionDto {
  @ApiProperty({ description: 'ID of the program user enrollment' })
  @IsString()
  programUserId: string;

  @ApiProperty({ description: 'ID of the content being interacted with' })
  @IsString()
  contentId: string;

  @ApiProperty({ enum: ProgressStatus, description: 'Current completion status' })
  @IsEnum(ProgressStatus)
  status: ProgressStatus;

  @ApiProperty({ description: 'When the user first accessed this content', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  startedAt?: Date;

  @ApiProperty({ description: 'When the user completed this content', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  completedAt?: Date;

  @ApiProperty({ description: 'Total time spent on this content in seconds', required: false })
  @IsOptional()
  @IsNumber()
  timeSpentSeconds?: number;

  @ApiProperty({ description: 'When the submission was received', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  submittedAt?: Date;

  @ApiProperty({ description: 'Structured data containing quiz answers', required: false })
  @IsOptional()
  @IsJSON()
  answers?: object;

  @ApiProperty({ description: 'Plain text submission for text-based assignments', required: false })
  @IsOptional()
  @IsString()
  textResponse?: string;

  @ApiProperty({ description: 'URL reference for external content submissions', required: false })
  @IsOptional()
  @IsString()
  urlResponse?: string;

  @ApiProperty({ description: 'Metadata for uploaded files', required: false })
  @IsOptional()
  @IsJSON()
  fileResponse?: object;

  @ApiProperty({ description: 'Additional data based on content type', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;
}
