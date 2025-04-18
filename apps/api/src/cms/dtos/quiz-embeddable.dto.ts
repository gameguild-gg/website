import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty } from 'class-validator';

export class QuizEmbeddableDto {
  @ApiProperty()
  @IsNotEmpty()
  title: string;

  @ApiProperty()
  @IsNotEmpty()
  markdown: string;

  @ApiProperty({ type: 'string', isArray: true })
  @IsNotEmpty()
  options: string[];

  @ApiProperty({ type: 'string', isArray: true })
  @IsNotEmpty()
  correctOptions: string[];
}
