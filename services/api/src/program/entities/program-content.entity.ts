import { Entity, Column, ManyToOne, OneToMany, Index, DeleteDateColumn, JoinColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsNotEmpty, IsOptional, IsEnum, IsNumber, IsBoolean, IsJSON, IsDate } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ProgramContentType, GradingMethod } from './enums';
import { Program } from './program.entity';
import { ContentInteraction } from './content-interaction.entity';

@Entity('program_contents')
@Index((entity) => [entity.program])
@Index((entity) => [entity.program, entity.order])
@Index((entity) => [entity.parent])
@Index((entity) => [entity.parent, entity.order])
@Index((entity) => [entity.type])
@Index((entity) => [entity.previewable])
@Index((entity) => [entity.dueDate])
@Index((entity) => [entity.availableFrom, entity.availableTo])
@Index((entity) => [entity.parent, entity.previewable])
export class ProgramContent extends EntityBase {
  @ApiProperty({ type: () => Program, description: 'Reference to the program this content belongs to' })
  @ManyToOne(() => Program, (program) => program.contents, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  program: Program;

  @ApiProperty({ type: () => ProgramContent, description: 'Self-referential for hierarchical structure, null if top-level content', required: false })
  @ManyToOne(() => ProgramContent, (programContent) => programContent.children, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'parent_id' })
  @IsOptional()
  parent: ProgramContent | null;

  @Column({ type: 'enum', enum: ProgramContentType })
  @ApiProperty({ enum: ProgramContentType, description: 'Type of content (page, quiz, assignment, discussion, etc.)' })
  @IsEnum(ProgramContentType)
  type: ProgramContentType;

  @Column({ type: 'varchar', length: 255 })
  @ApiProperty({ description: 'Title/name of the content shown in navigation and headers' })
  @IsNotEmpty()
  @IsString()
  title: string;

  @Column({ type: 'text', nullable: true })
  @ApiProperty({ description: 'Brief description of the content, shown in previews', required: false })
  @IsOptional()
  @IsString()
  summary: string | null;

  @Column({ type: 'float' })
  @ApiProperty({ description: 'Position value for sorting content, allows for flexible ordering' })
  @IsNumber()
  order: number;

  @Column({ type: 'jsonb' })
  @ApiProperty({ description: 'Main content in structured JSON format including text, questions, instructions, etc.' })
  @IsJSON()
  body: object;

  @Column({ type: 'boolean', default: false })
  @ApiProperty({ description: 'Flag to indicate if this content can be accessed without purchasing the program' })
  @IsBoolean()
  previewable: boolean;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'Deadline for completion, null if no deadline', required: false })
  @IsOptional()
  @IsDate()
  dueDate: Date | null;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When this content becomes available to users, null if always available', required: false })
  @IsOptional()
  @IsDate()
  availableFrom: Date | null;

  @Column({ type: 'timestamp', nullable: true })
  @ApiProperty({ description: 'When this content becomes unavailable, null if never expires', required: false })
  @IsOptional()
  @IsDate()
  availableTo: Date | null;

  @Column({ type: 'enum', enum: GradingMethod, nullable: true })
  @ApiProperty({ enum: GradingMethod, description: 'Method used to grade this content (null for pages)', required: false })
  @IsOptional()
  @IsEnum(GradingMethod)
  gradingMethod: GradingMethod | null;

  @Column({ type: 'int', nullable: true })
  @ApiProperty({ description: 'Time limit in minutes for timed activities, null means no time limit', required: false })
  @IsOptional()
  @IsNumber()
  durationMinutes: number | null;

  @Column({ type: 'boolean', default: false })
  @ApiProperty({ description: 'Whether text response is accepted for submission' })
  @IsBoolean()
  textResponse: boolean;

  @Column({ type: 'boolean', default: false })
  @ApiProperty({ description: 'Whether URL submission is accepted' })
  @IsBoolean()
  urlResponse: boolean;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({
    description: 'Allowed file extensions for uploads: null = not accepted, [] = any file, ["pdf","docx"] = only these extensions',
    required: false,
  })
  @IsOptional()
  @IsJSON()
  fileResponseExtensions: string[] | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Detailed rubric configuration for consistent grading, including criteria and point values', required: false })
  @IsOptional()
  @IsJSON()
  gradingRubric: object | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Flexible storage for content-specific settings based on type', required: false })
  @IsOptional()
  @IsJSON()
  metadata: object | null;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the content was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @ApiProperty({ type: () => ContentInteraction, isArray: true, description: 'User interactions with this content' })
  @OneToMany(() => ContentInteraction, (interaction) => interaction.content)
  interactions: ContentInteraction[];

  @ApiProperty({ type: () => ProgramContent, isArray: true, description: 'Child content items in hierarchical structure' })
  @OneToMany(() => ProgramContent, (programContent) => programContent.parent)
  children: ProgramContent[];
}
