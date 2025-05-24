import { Column, Entity, ManyToOne } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsDate, IsEnum, IsNotEmpty, IsNumber, IsOptional, IsString, Max, Min, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';

// Define the moderation status enum that is referenced in DBML
export enum ModerationStatus {
  PENDING = 'pending',
  APPROVED = 'approved',
  REJECTED = 'rejected',
  FLAGGED = 'flagged',
}

@Entity({ name: 'program_ratings' })
export class ProgramRatingEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { nullable: false })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  @ApiProperty({ description: 'Program being rated (null for product rating)' })
  @Column({ type: 'int', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Program ID must be a number' })
  program_id: number;

  @ApiProperty({ description: 'Product being rated (null for program rating)' })
  @Column({ type: 'int', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Product ID must be a number' })
  product_id: number;

  @ApiProperty({ description: 'Reference to program enrollment (for program ratings)' })
  @Column({ type: 'int', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Program user ID must be a number' })
  program_user_id: number;

  // Rating data
  @ApiProperty({ description: 'Numeric rating on 1-5 scale with decimals allowed (e.g., 4.5)' })
  @Column({ type: 'int', nullable: false })
  @IsNotEmpty({ message: 'Rating is required' })
  @IsNumber({}, { message: 'Rating must be a number' })
  @Min(1, { message: 'Rating must be at least 1' })
  @Max(5, { message: 'Rating must be at most 5' })
  rating: number;

  @ApiProperty({ description: 'Optional title for the review' })
  @Column({ type: 'varchar', nullable: true })
  @IsOptional()
  @IsString({ message: 'Review title must be a string' })
  review_title: string;

  @ApiProperty({ description: 'Optional detailed review text' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'Review text must be a string' })
  review_text: string;

  // Rating categories (optional detailed ratings)
  @ApiProperty({ description: 'Rating for content quality (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Content quality rating must be a number' })
  @Min(1, { message: 'Content quality rating must be at least 1' })
  @Max(5, { message: 'Content quality rating must be at most 5' })
  content_quality_rating: number;

  @ApiProperty({ description: 'Rating for instructor performance (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Instructor rating must be a number' })
  @Min(1, { message: 'Instructor rating must be at least 1' })
  @Max(5, { message: 'Instructor rating must be at most 5' })
  instructor_rating: number;

  @ApiProperty({ description: 'Rating for program difficulty (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Difficulty rating must be a number' })
  @Min(1, { message: 'Difficulty rating must be at least 1' })
  @Max(5, { message: 'Difficulty rating must be at most 5' })
  difficulty_rating: number;

  @ApiProperty({ description: 'Rating for value for money (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Value rating must be a number' })
  @Min(1, { message: 'Value rating must be at least 1' })
  @Max(5, { message: 'Value rating must be at most 5' })
  value_rating: number;

  // Metadata
  @ApiProperty({ description: 'When rating was submitted' })
  @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP' })
  @IsOptional()
  @IsDate({ message: 'Submitted at must be a valid date' })
  submitted_at: Date;

  @ApiProperty({ description: 'IP address of submission for fraud prevention' })
  @Column({ type: 'varchar', nullable: true })
  @IsOptional()
  @IsString({ message: 'IP address must be a string' })
  ip_address: string;

  @ApiProperty({ description: 'User agent string for analytics and fraud prevention' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'User agent must be a string' })
  user_agent: string;

  // Administration and moderation
  @ApiProperty({ description: 'Whether rating should be displayed publicly' })
  @Column({ type: 'boolean', default: true })
  @IsOptional()
  @IsBoolean({ message: 'Is public must be a boolean' })
  is_public: boolean;

  @ApiProperty({ description: 'Whether rating has been verified as legitimate' })
  @Column({ type: 'boolean', default: false })
  @IsOptional()
  @IsBoolean({ message: 'Is verified must be a boolean' })
  is_verified: boolean;

  @ApiProperty({ enum: ModerationStatus, description: 'Moderation status' })
  @Column({ type: 'enum', enum: ModerationStatus, default: ModerationStatus.PENDING })
  @IsOptional()
  @IsEnum(ModerationStatus, { message: 'Invalid moderation status' })
  moderation_status: ModerationStatus;

  @ApiProperty({ description: 'Notes from moderation process' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'Moderation notes must be a string' })
  moderation_notes: string;

  @ApiProperty({ type: () => UserEntity, description: 'User who moderated this rating' })
  @ManyToOne(() => UserEntity, { nullable: true })
  @IsOptional()
  @ValidateNested()
  @Type(() => UserEntity)
  moderated_by: UserEntity;

  @ApiProperty({ description: 'When moderation was completed' })
  @Column({ type: 'timestamp', nullable: true })
  @IsOptional()
  @IsDate({ message: 'Moderated at must be a valid date' })
  moderated_at: Date;
}
