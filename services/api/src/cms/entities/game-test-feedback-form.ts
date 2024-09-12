import { ApiProperty } from '@nestjs/swagger';
import {
  IsBoolean,
  IsEnum,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  MaxLength,
} from 'class-validator';
import { IsIntegerNumber } from '../../common/decorators/validator.decorator';

export enum GameTestFeedbackQuestionType {
  SHORT_ANSWER = 'SHORT_ANSWER',
  PARAGRAPH = 'PARAGRAPH',
  CHECKBOX = 'CHECKBOX',
  DROPDOWN = 'DROPDOWN',
  LINEAR_SCALE = 'LINEAR_SCALE',
}

// pass in the generic the type of the question
export class GameTestFeedbackQuestion {
  @ApiProperty({ enum: GameTestFeedbackQuestionType })
  @IsEnum(GameTestFeedbackQuestionType, {
    message: 'error.invalid_question_type: Invalid question type',
  })
  @IsNotEmpty({
    message: 'error.empty_question_type: Question type is required',
  })
  type: GameTestFeedbackQuestionType;

  @ApiProperty()
  @IsOptional()
  @MaxLength(1024, {
    message:
      'error.max_length_question_description: Question description must be at most 1024 characters',
  })
  description: string;

  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_is_required: IsRequired shold not be empty',
  })
  @IsBoolean({
    message: 'error.invalid_question_is_required: isRequired must be a boolean',
  })
  isRequired: boolean;

  constructor(partial: Partial<GameTestFeedbackQuestion>) {
    Object.assign(this, partial);
  }
}

export class GameTestFeedbackQuestionShortAnswer extends GameTestFeedbackQuestion {
  @ApiProperty()
  @IsOptional()
  minimumCharacters: number;

  @ApiProperty()
  @IsOptional()
  maximumCharacters: number;

  constructor(partial: Partial<GameTestFeedbackQuestionShortAnswer>) {
    super(partial);
  }
}

export class GameTestFeedbackQuestionParagraph extends GameTestFeedbackQuestionShortAnswer {
  @ApiProperty()
  @IsOptional()
  minimumLines: number;

  @ApiProperty()
  @IsOptional()
  maximumLines: number;
  constructor(partial: Partial<GameTestFeedbackQuestionParagraph>) {
    super(partial);
  }
}

export class GameTestFeedbackQuestionCheckbox extends GameTestFeedbackQuestion {
  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_options: Question options are required',
  })
  @IsString({
    each: true,
    message: 'error.invalid_question_options: Invalid question options',
  })
  @MaxLength(256, {
    each: true,
    message:
      'error.max_length_question_option: Question option must be at most 256 characters',
  })
  options: string[];

  constructor(partial: Partial<GameTestFeedbackQuestionCheckbox>) {
    super(partial);
  }
}

export class GameTestFeedbackQuestionDropdown extends GameTestFeedbackQuestion {
  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_options: Question options are required',
  })
  @IsString({
    each: true,
    message: 'error.invalid_question_options: Invalid question options',
  })
  @MaxLength(256, {
    each: true,
    message:
      'error.max_length_question_option: Question option must be at most 256 characters',
  })
  options: string[];

  constructor(partial: Partial<GameTestFeedbackQuestionDropdown>) {
    super(partial);
  }
}

export class GameTestFeedbackQuestionLinearScale extends GameTestFeedbackQuestion {
  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_min: Question min is required',
  })
  @IsNumber(
    {},
    { message: 'error.invalid_question_min: minimum must be a number' },
  )
  minimum: number;

  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_max: Question max is required',
  })
  @IsNumber(
    {},
    { message: 'error.invalid_question_max: maximum must be a number' },
  )
  maximum: number;

  @ApiProperty()
  @IsNotEmpty({
    message: 'error.empty_question_steps: Question steps are required',
  })
  @IsIntegerNumber({
    message: 'error.invalid_question_steps: steps must be an integer number',
  })
  steps: number;

  constructor(partial: Partial<GameTestFeedbackQuestionLinearScale>) {
    super(partial);
  }
}
