import { IsString, IsOptional, IsBoolean, IsEnum, IsDate } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ProgramContentType } from '../../entities/enums';

export class ContentFilters {
  @ApiProperty({ description: 'Filter by program ID', required: false })
  @IsOptional()
  @IsString()
  programId?: string;

  @ApiProperty({ description: 'Filter by parent content ID', required: false })
  @IsOptional()
  @IsString()
  parentId?: string;

  @ApiProperty({ enum: ProgramContentType, description: 'Filter by content type', required: false })
  @IsOptional()
  @IsEnum(ProgramContentType)
  type?: ProgramContentType;

  @ApiProperty({ description: 'Filter by previewable status', required: false })
  @IsOptional()
  @IsBoolean()
  previewable?: boolean;

  @ApiProperty({ description: 'Filter content available from this date', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  availableFrom?: Date;

  @ApiProperty({ description: 'Filter content available to this date', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  availableTo?: Date;
}
