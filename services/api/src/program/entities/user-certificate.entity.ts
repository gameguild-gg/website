import { Entity, Column, ManyToOne, JoinColumn, Index, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsUrl, IsEnum, IsDateString, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { CertificateStatus } from './enums';
import { UserEntity } from '../../user/entities/user.entity';
import { Program } from './program.entity';
import { Product } from './product.entity';
import { ProgramUser } from './program-user.entity';
import { Certificate } from './certificate.entity';

@Entity('user_certificates')
@Index((entity) => [entity.program])
@Index((entity) => [entity.product])
@Index((entity) => [entity.user])
@Index((entity) => [entity.programUser])
@Index((entity) => [entity.certificate])
@Index((entity) => [entity.certificateNumber], { unique: true })
@Index((entity) => [entity.status])
@Index((entity) => [entity.issuedAt])
@Index((entity) => [entity.expiresAt])
export class UserCertificate extends EntityBase {
  @ManyToOne(() => Program, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  @ApiProperty({ description: 'Reference to the program for single program certificates (null for multi-program certs)', required: false })
  @IsOptional()
  program?: Program;

  @ManyToOne(() => Product, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  @ApiProperty({ description: 'Reference to the product/bundle for product certificates (null for program-only certs)', required: false })
  @IsOptional()
  product?: Product;

  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  @ApiProperty({ description: 'User who earned the certificate' })
  user: UserEntity;

  @ManyToOne(() => ProgramUser, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_user_id' })
  @ApiProperty({ description: 'Program enrollment record if certificate is program-specific', required: false })
  @IsOptional()
  programUser?: ProgramUser;

  @ManyToOne(() => Certificate, (certificate) => certificate.userCertificates, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'certificate_id' })
  @ApiProperty({ type: () => Certificate, description: 'Reference to the certificate template/configuration' })
  certificate: Certificate;

  @Column('varchar', { length: 100, unique: true })
  @ApiProperty({ description: 'Unique certificate number for verification' })
  @IsString()
  @IsNotEmpty()
  certificateNumber: string;

  @Column({ type: 'enum', enum: CertificateStatus })
  @ApiProperty({ enum: CertificateStatus, description: 'Current status of the certificate' })
  @IsEnum(CertificateStatus)
  status: CertificateStatus;

  @Column('timestamp')
  @ApiProperty({ description: 'When the certificate was issued' })
  @IsDateString()
  issuedAt: Date;

  @Column('timestamp', { nullable: true })
  @ApiProperty({ description: 'When the certificate expires, null for no expiration', required: false })
  @IsOptional()
  @IsDateString()
  expiresAt?: Date;

  @Column('timestamp', { nullable: true })
  @ApiProperty({ description: 'When the certificate was revoked, null if never revoked', required: false })
  @IsOptional()
  @IsDateString()
  revokedAt?: Date;

  @Column('text', { nullable: true })
  @ApiProperty({ description: 'Reason for revocation if applicable', required: false })
  @IsOptional()
  @IsString()
  revocationReason?: string;

  @Column('varchar', { nullable: true })
  @ApiProperty({ description: 'URL where the certificate can be viewed or downloaded', required: false })
  @IsOptional()
  @IsUrl()
  certificateUrl?: string;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Additional data specific to the certificate instance', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp', required: false })
  deletedAt: Date | null;
}
