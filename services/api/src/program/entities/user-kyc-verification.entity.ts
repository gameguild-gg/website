import { Entity, Column, ManyToOne, JoinColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsEnum, IsDateString } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { KycProvider, KycVerificationStatus } from './enums';
import { UserEntity } from '../../user/entities/user.entity';

@Entity('user_kyc_verifications')
export class UserKycVerification extends EntityBase {
  @ApiProperty({ description: 'KYC service provider used for this verification' })
  @Column({ type: 'enum', enum: KycProvider })
  @IsEnum(KycProvider)
  provider: KycProvider;

  @ApiProperty({ description: 'Provider-specific applicant identifier' })
  @Column('varchar', { unique: true })
  @IsString()
  @IsNotEmpty()
  providerApplicantId: string;

  @ApiProperty({ description: 'Provider-specific verification identifier' })
  @Column('varchar', { unique: true })
  @IsString()
  @IsNotEmpty()
  providerVerificationId: string;

  @ApiProperty({ description: 'Level of verification completed (basic, advanced, etc.)' })
  @Column('varchar')
  @IsString()
  @IsNotEmpty()
  verificationLevel: string;

  @ApiProperty({ description: 'Current status of the verification', default: 'pending' })
  @Column({ type: 'enum', enum: KycVerificationStatus, default: KycVerificationStatus.PENDING })
  @IsEnum(KycVerificationStatus)
  status: KycVerificationStatus;

  @ApiProperty({ description: 'When the verification status was last checked with provider' })
  @Column('timestamp')
  @IsDateString()
  lastCheckAt: Date;

  @ApiProperty({ description: 'When the verification was approved', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  approvedAt?: Date;

  @ApiProperty({ description: 'When the verification was rejected', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  rejectedAt?: Date;

  @ApiProperty({ description: 'If rejected, reason provided by the provider (no personal data)', required: false })
  @Column('text', { nullable: true })
  @IsString()
  @IsOptional()
  rejectionReason?: string;

  @ApiProperty({ description: 'When this verification expires and needs renewal', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  expiresAt?: Date;

  @ApiProperty({ description: 'Provider-specific data and configuration', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  metadata?: Record<string, any>;

  // Relations
  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  user: UserEntity;
}
