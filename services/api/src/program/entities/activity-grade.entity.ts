import { Column, Entity, Index, JoinColumn, ManyToOne } from 'typeorm';
import { IsDate, IsNumber, IsOptional, IsString } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ContentInteraction } from './content-interaction.entity';
import { ProgramUser } from './program-user.entity';
import { ApiProperty } from '@nestjs/swagger';

@Entity('activity_grades')
@Index((grade) => [grade.contentInteraction], { unique: true })
export class ActivityGrade extends EntityBase {
  @Column('decimal', { precision: 5, scale: 2, nullable: true })
  @IsNumber()
  @IsOptional()
  @ApiProperty({ type: Number, nullable: true })
  grade?: number;

  @Column('timestamp', { nullable: true })
  @IsOptional()
  @IsDate()
  @ApiProperty({ type: Date, nullable: true })
  gradedAt?: Date;

  @Column('text', { nullable: true })
  @IsString()
  @IsOptional()
  @ApiProperty({ type: String, nullable: true, description: 'Feedback for the activity grade' })
  feedback?: string;

  @Column('jsonb', { nullable: true })
  @IsOptional()
  @ApiProperty({ type: Object, nullable: true, description: 'Rubric assessment data in JSON format' })
  rubricAssessment?: object;

  @Column('jsonb', { nullable: true })
  @IsOptional()
  @ApiProperty({ type: Object, nullable: true, description: 'Additional metadata in JSON format' })
  metadata?: object;

  // Relations
  @ManyToOne(() => ContentInteraction, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'content_interaction_id' })
  @ApiProperty({ type: () => ContentInteraction, description: 'The content interaction being graded' })
  contentInteraction: ContentInteraction;

  @ManyToOne(() => ProgramUser, { nullable: true })
  @JoinColumn({ name: 'grader_program_user_id' })
  @ApiProperty({ type: () => ProgramUser, nullable: true, description: 'The program user who provided the grade' })
  graderProgramUser?: ProgramUser;
}
