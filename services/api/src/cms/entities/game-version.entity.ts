import { Column, Entity, Index, ManyToOne, OneToMany, Unique } from 'typeorm';
import { ApiExtraModels, ApiProperty, getSchemaPath } from '@nestjs/swagger';
import { GameEntity } from './game.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { GameFeedbackResponseEntity } from './game-feedback-response.entity';
import {
  IsArray,
  IsDate,
  IsNotEmpty,
  IsSemVer,
  IsString,
  IsUrl,
  MaxLength,
  ValidateNested,
} from 'class-validator';
import { Type } from 'class-transformer';
import {
  GameTestFeedbackQuestion,
  GameTestFeedbackQuestionCheckbox,
  GameTestFeedbackQuestionDropdown,
  GameTestFeedbackQuestionLinearScale,
  GameTestFeedbackQuestionParagraph,
  GameTestFeedbackQuestionShortAnswer,
} from './game-test-feedback-form';

@ApiExtraModels(
  GameTestFeedbackQuestion,
  GameTestFeedbackQuestionCheckbox,
  GameTestFeedbackQuestionDropdown,
  GameTestFeedbackQuestionLinearScale,
  GameTestFeedbackQuestionShortAnswer,
  GameTestFeedbackQuestionParagraph,
)
@Entity('game_version')
@Unique(['version', 'game'])
export class GameVersionEntity extends EntityBase {
  @ApiProperty()
  @IsString({ message: 'Version must be a string' })
  @IsNotEmpty({ message: 'Version is required' })
  @MaxLength(64, { message: 'Version must be at most 64 characters' })
  @IsSemVer({ message: 'Version must be a valid SemVer string' })
  @Column({ length: 64, nullable: false })
  version: string;

  @ApiProperty()
  @IsString({ message: 'Archive URL must be a string' })
  @IsNotEmpty({ message: 'Archive URL is required' })
  @IsUrl({}, { message: 'Archive URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Archive URL must be at most 1024 characters' })
  @Column({ length: 1024, nullable: false })
  archive_url: string;

  // notes / testing plan
  @ApiProperty()
  @IsString({ message: 'Notes URL must be a string' })
  @IsNotEmpty({ message: 'Notes URL is required' })
  @IsUrl({}, { message: 'Notes URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Notes URL must be at most 1024 characters' })
  @Column({ length: 1024, nullable: false })
  notes_url: string;

  // feedback form
  // type can be any of these GameTestFeedbackQuestion, GameTestFeedbackQuestionCheckbox, GameTestFeedbackQuestionDropdown, GameTestFeedbackQuestionLinearScale, GameTestFeedbackQuestionShortAnswer, GameTestFeedbackQuestionParagraph
  @ApiProperty({
    // error: Could not resolve reference: Could not resolve pointer: /components/schemas/GameTestFeedbackQuestion does not exist in document
    oneOf: [
      { $ref: getSchemaPath(GameTestFeedbackQuestion) },
      { $ref: getSchemaPath(GameTestFeedbackQuestionCheckbox) },
      { $ref: getSchemaPath(GameTestFeedbackQuestionDropdown) },
      { $ref: getSchemaPath(GameTestFeedbackQuestionLinearScale) },
      { $ref: getSchemaPath(GameTestFeedbackQuestionShortAnswer) },
      { $ref: getSchemaPath(GameTestFeedbackQuestionParagraph) },
    ],
  })
  @Type(() => GameTestFeedbackQuestion)
  @IsNotEmpty({ message: 'Feedback form is required' })
  @ValidateNested()
  @Column({ type: 'jsonb', nullable: false })
  feedback_form: (
    | GameTestFeedbackQuestion
    | GameTestFeedbackQuestionCheckbox
    | GameTestFeedbackQuestionDropdown
    | GameTestFeedbackQuestionLinearScale
    | GameTestFeedbackQuestionShortAnswer
    | GameTestFeedbackQuestionParagraph
  )[];

  // deadline
  @ApiProperty()
  @IsNotEmpty({ message: 'Feedback deadline is required' })
  @IsDate({ message: 'Feedback deadline must be a date' })
  @Column({ type: 'timestamptz', nullable: false })
  @Index({ unique: false })
  feedback_deadline: Date;

  @ApiProperty({ type: () => GameEntity })
  @ManyToOne(() => GameEntity, (game) => game.versions)
  @ValidateNested()
  @Type(() => GameEntity)
  game: GameEntity;

  // relation to feedback responses
  @ApiProperty({ type: GameFeedbackResponseEntity, isArray: true })
  @OneToMany(() => GameFeedbackResponseEntity, (response) => response.version)
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => GameFeedbackResponseEntity)
  responses: GameFeedbackResponseEntity[];

  constructor(partial: Partial<GameVersionEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
