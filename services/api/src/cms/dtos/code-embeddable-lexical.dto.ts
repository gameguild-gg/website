import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsOptional, Length, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { CodeLanguage } from './code-activity.dto';

export class EmbeddablecedCodeActivityTest {
  @ApiProperty()
  in: string;

  @ApiProperty()
  out: string;
}

// to be used inside lectures or any other content
export class EmbeddableCodeActivityDto {
  @ApiProperty()
  @IsOptional()
  @Length(3, 255)
  title: string;

  @ApiProperty({ enum: CodeLanguage })
  @IsEnum(CodeLanguage)
  @IsNotEmpty()
  language: CodeLanguage;

  @ApiProperty()
  @IsNotEmpty()
  starterCode: string;

  @ApiProperty({ type: EmbeddablecedCodeActivityTest, isArray: true })
  @IsOptional()
  @ValidateNested({ each: true })
  @Type(() => EmbeddablecedCodeActivityTest)
  tests: EmbeddablecedCodeActivityTest[];
}
