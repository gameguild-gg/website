import { QuizQuestionBase } from './question.base';
import {
  IsEnum,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  MaxLength,
} from 'class-validator';
import { IsIntegerNumber } from '../../common/decorators/validator.decorator';
import { ApiProperty } from '@nestjs/swagger';
import { FormQuestionEnum } from './form-question.enum';

export class FormLongAnswerDto implements QuizQuestionBase {
  @IsNotEmpty()
  @IsString()
  @MaxLength(1024)
  @ApiProperty()
  question: string;

  @IsNotEmpty()
  @IsNumber()
  @ApiProperty()
  order: number;

  @IsOptional()
  @IsNumber()
  @ApiProperty()
  points?: number;

  @IsOptional()
  @IsIntegerNumber()
  @ApiProperty({ type: 'integer' })
  minLimit?: number;

  @IsOptional()
  @IsIntegerNumber()
  @ApiProperty({ type: 'integer' })
  maxLimit?: number;

  @IsOptional()
  @IsString()
  // todo: exclude from response
  @ApiProperty()
  answer?: string;

  // todo: type should be 'short-answer' always. We need to protect this field from being changed
  @ApiProperty({
    enum: FormQuestionEnum,
    default: FormQuestionEnum.LongAnswer,
  })
  @IsNotEmpty()
  @IsEnum(FormQuestionEnum)
  type: FormQuestionEnum = FormQuestionEnum.LongAnswer;
}
