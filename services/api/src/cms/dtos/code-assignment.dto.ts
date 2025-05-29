import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsOptional, Length, ValidateNested } from 'class-validator';
import { CodeLanguage } from './code-activity.dto';
import { Type } from 'class-transformer';
import { FileDto } from './file.dto';
import { EmbeddablecedCodeActivityTest } from './code-embeddable-lexical.dto';

// to be used as assignment for lectures
export class CodeAssignmentDto {
  @ApiProperty()
  @IsNotEmpty()
  @Length(3, 255)
  title: string;

  @ApiProperty()
  @IsNotEmpty()
  markdown: string;

  @ApiProperty({ type: 'object' })
  @IsNotEmpty()
  lexical: Record<string, any>;

  @ApiProperty({ enum: CodeLanguage })
  @IsEnum(CodeLanguage)
  @IsNotEmpty()
  language: CodeLanguage;

  @ApiProperty({ type: FileDto, isArray: true })
  @IsNotEmpty()
  files: FileDto[];

  @ApiProperty({ type: EmbeddablecedCodeActivityTest, isArray: true })
  @IsOptional()
  @ValidateNested({ each: true })
  @Type(() => EmbeddablecedCodeActivityTest)
  tests: EmbeddablecedCodeActivityTest[];
}
