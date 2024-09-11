import { ApiProperty } from '@nestjs/swagger';
import {
  IsBoolean,
  IsEnum,
  IsNotEmpty,
  IsOptional,
  MaxLength,
} from 'class-validator';

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
