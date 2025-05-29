import { IsString, IsNumber, IsBoolean, IsOptional, IsDate, IsEnum } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ProgramContentType, ProgressStatus } from '../../entities/enums';

export class ContentProgress {
  @ApiProperty({ description: 'ID of the content' })
  @IsString()
  contentId: string;

  @ApiProperty({ description: 'Title of the content' })
  @IsString()
  title: string;

  @ApiProperty({ enum: ProgramContentType, description: 'Type of the content' })
  @IsEnum(ProgramContentType)
  type: ProgramContentType;

  @ApiProperty({ enum: ProgressStatus, description: 'Current completion status' })
  @IsEnum(ProgressStatus)
  status: ProgressStatus;

  @ApiProperty({ description: 'Total time spent on this content in seconds' })
  @IsNumber()
  timeSpentSeconds: number;

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

  @ApiProperty({ description: 'When the user last accessed this content', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  lastAccessedAt?: Date;

  @ApiProperty({ description: 'Duration limit for this content in minutes', required: false })
  @IsOptional()
  @IsNumber()
  durationMinutes?: number;
}
