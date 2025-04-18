import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsOptional, Length, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { CodeLanguageEnum } from './code.language.enum';

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

  @ApiProperty({ enum: CodeLanguageEnum })
  @IsEnum(CodeLanguageEnum)
  @IsNotEmpty()
  language: CodeLanguageEnum;

  @ApiProperty()
  @IsNotEmpty()
  starterCode: string;

  @ApiProperty({ type: EmbeddablecedCodeActivityTest, isArray: true })
  @IsOptional()
  @ValidateNested({ each: true })
  @Type(() => EmbeddablecedCodeActivityTest)
  tests: EmbeddablecedCodeActivityTest[];
}
