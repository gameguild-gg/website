import { Column, Entity, Index, ManyToOne, OneToMany, Unique } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { GameEntity } from './game.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { GameFeedbackResponseEntity } from './game-feedback-response.entity';
import {
  IsDate,
  IsFQDN,
  IsNotEmpty,
  IsSemVer,
  IsString,
  MaxLength,
} from 'class-validator';

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
  @IsFQDN({}, { message: 'Archive URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Archive URL must be at most 1024 characters' })
  @Column({ length: 1024, nullable: false })
  archive_url: string;

  // notes / testing plan
  @ApiProperty()
  @IsString({ message: 'Notes URL must be a string' })
  @IsNotEmpty({ message: 'Notes URL is required' })
  @IsFQDN({}, { message: 'Notes URL must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Notes URL must be at most 1024 characters' })
  @Column({ length: 1024, nullable: false })
  notes_url: string;

  // feedback form
  @ApiProperty()
  @IsNotEmpty({ message: 'Feedback form is required' })
  @IsString({ message: 'Feedback form must be a string' })
  @IsFQDN({}, { message: 'Feedback form must be a fully qualified URL' })
  @MaxLength(1024, { message: 'Feedback form must be at most 1024 characters' })
  @Column({ length: 1024, nullable: false })
  feedback_form: string;

  // deadline
  @ApiProperty()
  @IsNotEmpty({ message: 'Feedback deadline is required' })
  @IsDate({ message: 'Feedback deadline must be a date' })
  @Column({ type: 'timestamptz', nullable: false })
  @Index({ unique: false })
  feedback_deadline: Date;

  @ApiProperty({ type: () => GameEntity })
  @ManyToOne(() => GameEntity, (game) => game.versions)
  game: GameEntity;

  // relation to feedback responses
  @ApiProperty({ type: () => GameFeedbackResponseEntity, isArray: true })
  @OneToMany(() => GameFeedbackResponseEntity, (response) => response.version)
  responses: GameFeedbackResponseEntity[];
}
