import { Column, Entity, ManyToOne, Index } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsDate, IsEnum, IsNotEmpty, IsNumber, IsOptional, IsString, Max, Min, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';
import { Program } from './program.entity';
import { Product } from './product.entity';
import { ProgramUser } from './program-user.entity';
import { ModerationStatus } from './enums';

@Entity({ name: 'program_ratings' })
@Index((entity) => [entity.user, entity.program], { unique: true, where: 'program_id IS NOT NULL' })
@Index((entity) => [entity.user, entity.product], { unique: true, where: 'product_id IS NOT NULL' })
@Index((entity) => [entity.program, entity.rating])
@Index((entity) => [entity.product, entity.rating])
@Index((entity) => [entity.rating, entity.submittedAt])
@Index((entity) => [entity.isPublic, entity.moderationStatus])
@Index((entity) => [entity.moderationStatus, entity.submittedAt])
export class ProgramRatingEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { nullable: false })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  @ApiProperty({ type: () => Program, description: 'Program being rated (null for product rating)' })
  @ManyToOne(() => Program, { nullable: true })
  @ValidateNested()
  @Type(() => Program)
  program: Program | null;

  @ApiProperty({ type: () => Product, description: 'Product being rated (null for program rating)' })
  @ManyToOne(() => Product, { nullable: true })
  @ValidateNested()
  @Type(() => Product)
  product: Product | null;

  @ApiProperty({ type: () => ProgramUser, description: 'Reference to program enrollment (for program ratings)' })
  @ManyToOne(() => ProgramUser, { nullable: true })
  @ValidateNested()
  @Type(() => ProgramUser)
  programUser: ProgramUser | null;

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
  reviewTitle: string;

  @ApiProperty({ description: 'Optional detailed review text' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'Review text must be a string' })
  reviewText: string;

  // Rating categories (optional detailed ratings)
  @ApiProperty({ description: 'Rating for content quality (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Content quality rating must be a number' })
  @Min(1, { message: 'Content quality rating must be at least 1' })
  @Max(5, { message: 'Content quality rating must be at most 5' })
  contentQualityRating: number;

  @ApiProperty({ description: 'Rating for instructor performance (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Instructor rating must be a number' })
  @Min(1, { message: 'Instructor rating must be at least 1' })
  @Max(5, { message: 'Instructor rating must be at most 5' })
  instructorRating: number;

  @ApiProperty({ description: 'Rating for program difficulty (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Difficulty rating must be a number' })
  @Min(1, { message: 'Difficulty rating must be at least 1' })
  @Max(5, { message: 'Difficulty rating must be at most 5' })
  difficultyRating: number;

  @ApiProperty({ description: 'Rating for value for money (1-5)' })
  @Column({ type: 'float', nullable: true })
  @IsOptional()
  @IsNumber({}, { message: 'Value rating must be a number' })
  @Min(1, { message: 'Value rating must be at least 1' })
  @Max(5, { message: 'Value rating must be at most 5' })
  valueRating: number;

  // Metadata
  @ApiProperty({ description: 'When rating was submitted' })
  @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP' })
  @IsOptional()
  @IsDate({ message: 'Submitted at must be a valid date' })
  submittedAt: Date;

  @ApiProperty({ description: 'IP address of submission for fraud prevention' })
  @Column({ type: 'varchar', nullable: true })
  @IsOptional()
  @IsString({ message: 'IP address must be a string' })
  ipAddress: string;

  @ApiProperty({ description: 'User agent string for analytics and fraud prevention' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'User agent must be a string' })
  userAgent: string;

  // Administration and moderation
  @ApiProperty({ description: 'Whether rating should be displayed publicly' })
  @Column({ type: 'boolean', default: true })
  @IsOptional()
  @IsBoolean({ message: 'Is public must be a boolean' })
  isPublic: boolean;

  @ApiProperty({ description: 'Whether rating has been verified as legitimate' })
  @Column({ type: 'boolean', default: false })
  @IsOptional()
  @IsBoolean({ message: 'Is verified must be a boolean' })
  isVerified: boolean;

  @ApiProperty({ enum: ModerationStatus, description: 'Moderation status' })
  @Column({ type: 'enum', enum: ModerationStatus, default: ModerationStatus.PENDING })
  @IsOptional()
  @IsEnum(ModerationStatus, { message: 'Invalid moderation status' })
  moderationStatus: ModerationStatus;

  @ApiProperty({ description: 'Notes from moderation process' })
  @Column({ type: 'text', nullable: true })
  @IsOptional()
  @IsString({ message: 'Moderation notes must be a string' })
  moderationNotes: string;

  @ApiProperty({ type: () => UserEntity, description: 'User who moderated this rating' })
  @ManyToOne(() => UserEntity, { nullable: true })
  @IsOptional()
  @ValidateNested()
  @Type(() => UserEntity)
  moderatedBy: UserEntity;

  @ApiProperty({ description: 'When moderation was completed' })
  @Column({ type: 'timestamp', nullable: true })
  @IsOptional()
  @IsDate({ message: 'Moderated at must be a valid date' })
  moderatedAt: Date;
}
