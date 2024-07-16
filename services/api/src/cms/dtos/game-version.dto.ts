import { ApiProperty } from '@nestjs/swagger';
import { EntityDto } from '../../dtos/entity.dto';
import {
  IsDate,
  IsDateString,
  IsFQDN,
  IsNotEmpty,
  IsSemVer,
  IsString,
  MaxLength,
} from 'class-validator';
import { GameDto } from './game.dto';
import { GameFeedbackResponseDto } from './game-feedback-response.dto';

export class GameVersionDto extends EntityDto {
  @ApiProperty()
  @IsString({ message: 'Version must be a string' })
  @IsNotEmpty({ message: 'Version is required' })
  @MaxLength(64, { message: 'Version must be at most 64 characters' })
  @IsSemVer({ message: 'Version must be a valid SemVer string' })
  version: string;

  @ApiProperty()
  @IsString({ message: 'Archive URL must be a string' })
  @IsNotEmpty({ message: 'Archive URL is required' })
  @IsFQDN({}, { message: 'Archive URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Archive URL must be at most 1024 characters' })
  archive_url: string;

  // notes / testing plan
  @ApiProperty()
  @IsNotEmpty({ message: 'Notes URL is required' })
  @IsString({ message: 'Notes URL must be a string' })
  @IsFQDN({}, { message: 'Notes URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Notes URL must be at most 1024 characters' })
  notes_url: string;

  // feedback form
  @ApiProperty()
  @IsNotEmpty({ message: 'Feedback form is required' })
  @IsString({ message: 'Feedback form must be a string' })
  @IsFQDN({}, { message: 'Feedback form must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Feedback form must be at most 1024 characters' })
  feedback_form: string;

  // deadline
  @ApiProperty()
  @IsNotEmpty({ message: 'Feedback deadline is required' })
  @IsDate({ message: 'Feedback deadline must be a date' })
  feedback_deadline: Date;

  @ApiProperty({ type: () => GameDto })
  @IsNotEmpty({ message: 'Game is required' })
  game: GameDto;

  // relation to feedback responses
  @ApiProperty({ type: () => GameFeedbackResponseDto, isArray: true })
  responses: GameFeedbackResponseDto[];
}
