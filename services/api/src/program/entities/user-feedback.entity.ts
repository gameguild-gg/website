import { Entity, Column, ManyToOne, JoinColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsBoolean, IsIP } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';
import { Program } from './program.entity';
import { Product } from './product.entity';
import { ProgramUser } from './program-user.entity';

@Entity('program_feedback_submissions')
export class ProgramFeedbackSubmission extends EntityBase {
  @ApiProperty({ description: 'Structured responses to feedback form questions' })
  @Column('jsonb')
  feedbackResponses: Record<string, any>;

  @ApiProperty({ description: 'Version of feedback template used to ensure consistency' })
  @Column('varchar')
  @IsString()
  @IsNotEmpty()
  feedbackTemplateVersion: string;

  @ApiProperty({ description: 'When feedback was submitted' })
  @Column('timestamp', { default: () => 'now()' })
  submittedAt: Date;

  @ApiProperty({ description: 'IP address of submission for fraud prevention', required: false })
  @Column('varchar', { nullable: true })
  @IsIP()
  @IsOptional()
  ipAddress?: string;

  @ApiProperty({ description: 'User agent string for analytics and fraud prevention', required: false })
  @Column('text', { nullable: true })
  @IsString()
  @IsOptional()
  userAgent?: string;

  @ApiProperty({ description: 'Whether this submission is considered valid', default: true })
  @Column('boolean', { default: true })
  @IsBoolean()
  isValid: boolean;

  // Relations
  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  user: UserEntity;

  @ManyToOne(() => Program, { nullable: true })
  @JoinColumn({ name: 'program_id' })
  program?: Program;

  @ManyToOne(() => Product, { nullable: true })
  @JoinColumn({ name: 'product_id' })
  product?: Product;

  @ManyToOne(() => ProgramUser, { nullable: true })
  @JoinColumn({ name: 'program_user_id' })
  programUser?: ProgramUser;
}
