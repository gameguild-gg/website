import { Entity, Column, OneToMany, ManyToOne, ManyToMany, JoinColumn, JoinTable, Index, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsBoolean, IsUrl, IsEnum, IsNumber, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { Program } from './program.entity';
import { Product } from './product.entity';
import { UserCertificate } from './user-certificate.entity';
import { TagProficiency } from './tag-proficiency.entity';
import { CertificateType, VerificationMethod } from './enums';

@Entity('certificates')
@Index((entity) => [entity.program])
@Index((entity) => [entity.product])
@Index((entity) => [entity.certificateType])
@Index((entity) => [entity.isActive])
export class Certificate extends EntityBase {
  @ApiProperty({ type: () => Program, description: 'Reference to specific program (null for multi-program certificates)', required: false })
  @ManyToOne(() => Program, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  @IsOptional()
  program?: Program;

  @ApiProperty({ type: () => Product, description: 'Reference to specific product/bundle (null for cross-product certificates)', required: false })
  @ManyToOne(() => Product, { nullable: true, onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  @IsOptional()
  product?: Product;

  @Column({ type: 'enum', enum: CertificateType, default: CertificateType.PROGRAM_COMPLETION })
  @ApiProperty({ enum: CertificateType, description: 'What this certificate type proves when earned' })
  @IsEnum(CertificateType)
  certificateType: CertificateType;

  @Column('varchar', { length: 255 })
  @ApiProperty({ description: 'Display name for this certificate type' })
  @IsString()
  @IsNotEmpty()
  name: string;

  @Column('text')
  @ApiProperty({ description: 'Description of what this certificate represents' })
  @IsString()
  @IsNotEmpty()
  description: string;

  // Template and styling
  @Column('text')
  @ApiProperty({ description: 'HTML template for certificate generation with variable placeholders like {{name}}, {{date}}, {{program}}' })
  @IsString()
  @IsNotEmpty()
  htmlTemplate: string;

  @Column('text')
  @ApiProperty({ description: 'CSS styles for certificate template to control appearance, fonts, colors, etc.' })
  @IsString()
  @IsNotEmpty()
  cssStyles: string;

  // Issuance rules
  @Column('boolean', { default: true })
  @ApiProperty({ description: 'Whether certificates are automatically issued upon completion or require manual approval' })
  @IsBoolean()
  autoIssue: boolean;

  @Column('float', { default: 70 })
  @ApiProperty({ description: 'Minimum grade percentage required to earn a certificate (0-100)' })
  @IsNumber()
  minimumGrade: number;

  @Column('int', { default: 100 })
  @ApiProperty({ description: 'Percentage of required content that must be completed (0-100)' })
  @IsNumber()
  completionPercentage: number;

  @Column('boolean', { default: false })
  @ApiProperty({ description: 'Whether certificate requires submission of feedback form' })
  @IsBoolean()
  requiresFeedback: boolean;

  @Column('boolean', { default: false })
  @ApiProperty({ description: 'Whether certificate requires program rating submission' })
  @IsBoolean()
  requiresRating: boolean;

  @Column('float', { nullable: true })
  @ApiProperty({ description: 'Minimum rating required if rating is mandatory (1-5 scale), null means any rating accepted', required: false })
  @IsOptional()
  @IsNumber()
  minimumRating?: number;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'JSON template defining feedback form structure when requires_feedback is true', required: false })
  @IsOptional()
  @IsJSON()
  feedbackFormTemplate?: object;

  // Certificate details
  @Column('int', { nullable: true })
  @ApiProperty({ description: 'Number of months until certificate expires, null for no expiration', required: false })
  @IsOptional()
  @IsNumber()
  expirationMonths?: number;

  @Column({ type: 'enum', enum: VerificationMethod, default: VerificationMethod.CODE })
  @ApiProperty({ enum: VerificationMethod, description: 'Method used to verify certificate authenticity' })
  @IsEnum(VerificationMethod)
  certificateVerificationMethod: VerificationMethod;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Required achievements/programs before certificate can be issued', required: false })
  @IsOptional()
  @IsJSON()
  prerequisites?: object;

  @Column('varchar', { nullable: true })
  @ApiProperty({ description: 'URL or path to digital badge image for social sharing and profiles', required: false })
  @IsOptional()
  @IsUrl()
  badgeImage?: string;

  @Column('varchar', { nullable: true })
  @ApiProperty({ description: 'URL or path to authorizing signature image', required: false })
  @IsOptional()
  @IsUrl()
  signatureImage?: string;

  @Column('varchar', { nullable: true })
  @ApiProperty({ description: 'Professional title/credential granted by this certificate', required: false })
  @IsOptional()
  @IsString()
  credentialTitle?: string;

  @Column('varchar', { nullable: true })
  @ApiProperty({ description: 'Name of institution or entity issuing the certificate', required: false })
  @IsOptional()
  @IsString()
  issuerName?: string;

  // Metadata and administration
  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Additional configuration: custom fields, internationalization, visibility, etc.', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @Column('boolean', { default: true })
  @ApiProperty({ description: 'Whether this certificate template is currently active' })
  @IsBoolean()
  isActive: boolean;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp', required: false })
  deletedAt: Date | null;

  // Relations
  @OneToMany(() => UserCertificate, (userCert) => userCert.certificate)
  userCertificates: UserCertificate[];

  @ManyToMany(() => TagProficiency, (tagProficiency) => tagProficiency.certificates)
  @JoinTable({
    name: 'certificate_tags',
    joinColumn: { name: 'certificate_id', referencedColumnName: 'id' },
    inverseJoinColumn: { name: 'tag_id', referencedColumnName: 'id' },
  })
  @ApiProperty({ type: () => TagProficiency, isArray: true, description: 'Tag proficiencies associated with this certificate' })
  tagProficiencies: TagProficiency[];
}
