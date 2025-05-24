import { Entity, Column, ManyToOne, OneToMany, JoinColumn, Index, DeleteDateColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsOptional, IsEnum, IsNumber, IsJSON, IsDate } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ProgressStatus } from './enums';
import { ProgramUser } from './program-user.entity';
import { ProgramContent } from './program-content.entity';
import { ActivityGrade } from './activity-grade.entity';

@Entity('content_interactions')
@Index((interaction) => [interaction.programUser, interaction.content], { unique: true })
@Index((interaction) => [interaction.programUser, interaction.status])
@Index((interaction) => [interaction.content, interaction.status])
@Index((interaction) => [interaction.submittedAt])
@Index((interaction) => [interaction.completedAt])
export class ContentInteraction extends EntityBase {
  @Column({ type: 'enum', enum: ProgressStatus, default: ProgressStatus.NOT_STARTED })
  @ApiProperty({ enum: ProgressStatus, description: 'Current completion status' })
  @IsEnum(ProgressStatus)
  status: ProgressStatus;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When the user first accessed this content', required: false })
  @IsOptional()
  @IsDate()
  startedAt: Date | null;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When the user completed this content', required: false })
  @IsOptional()
  @IsDate()
  completedAt: Date | null;

  @Column({ type: 'int', default: 0 })
  @ApiProperty({ description: 'Total time spent on this content' })
  @IsNumber()
  timeSpentSeconds: number;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When the user last accessed this content', required: false })
  @IsOptional()
  @IsDate()
  lastAccessedAt: Date | null;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When the submission was received, null if no submission yet', required: false })
  @IsOptional()
  @IsDate()
  submittedAt: Date | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Structured data containing quiz answers with question IDs and selected responses', required: false })
  @IsOptional()
  @IsJSON()
  answers: object | null;

  @Column({ type: 'text', nullable: true })
  @ApiProperty({ description: 'Plain text submission for text-based assignments', required: false })
  @IsOptional()
  @IsString()
  textResponse: string | null;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'URL reference for external content submissions', required: false })
  @IsOptional()
  @IsString()
  urlResponse: string | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Metadata for uploaded files including paths, types, sizes, etc.', required: false })
  @IsOptional()
  @IsJSON()
  fileResponse: object | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Additional data based on content type', required: false })
  @IsOptional()
  @IsJSON()
  metadata: object | null;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the record was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @ManyToOne(() => ProgramUser, (programUser) => programUser.contentInteractions, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_user_id' })
  @ApiProperty({ description: 'Reference to the program enrollment' })
  programUser: ProgramUser;

  @ManyToOne(() => ProgramContent, (content) => content.interactions, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'content_id' })
  @ApiProperty({ description: 'Reference to the program content item being interacted with' })
  content: ProgramContent;

  @OneToMany(() => ActivityGrade, (grade) => grade.contentInteraction)
  grades: ActivityGrade[];
}
