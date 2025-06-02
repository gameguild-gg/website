import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsOptional, IsJSON, IsNumber, IsBoolean, IsEnum, IsUrl } from 'class-validator';
import { CertificateType, VerificationMethod } from '../../entities/enums';

export class CreateCertificateTemplateDto {
  @ApiProperty({ description: 'Program ID this certificate is associated with', required: false })
  @IsOptional()
  @IsString()
  programId?: string;

  @ApiProperty({ description: 'Product ID this certificate is associated with', required: false })
  @IsOptional()
  @IsString()
  productId?: string;

  @ApiProperty({ enum: CertificateType, description: 'Type of certificate' })
  @IsEnum(CertificateType)
  certificateType: CertificateType;

  @ApiProperty({ description: 'Display name for this certificate' })
  @IsString()
  name: string;

  @ApiProperty({ description: 'Description of what this certificate represents' })
  @IsString()
  description: string;

  @ApiProperty({ description: 'HTML template for certificate generation' })
  @IsString()
  htmlTemplate: string;

  @ApiProperty({ description: 'CSS styles for certificate template' })
  @IsString()
  cssStyles: string;

  @ApiProperty({ description: 'Whether certificates are automatically issued', required: false, default: true })
  @IsOptional()
  @IsBoolean()
  autoIssue?: boolean;

  @ApiProperty({ description: 'Minimum grade percentage required (0-100)', required: false, default: 70 })
  @IsOptional()
  @IsNumber()
  minimumGrade?: number;

  @ApiProperty({ description: 'Completion percentage required (0-100)', required: false, default: 100 })
  @IsOptional()
  @IsNumber()
  completionPercentage?: number;

  @ApiProperty({ description: 'Whether feedback is required', required: false, default: false })
  @IsOptional()
  @IsBoolean()
  requiresFeedback?: boolean;

  @ApiProperty({ description: 'Whether rating is required', required: false, default: false })
  @IsOptional()
  @IsBoolean()
  requiresRating?: boolean;

  @ApiProperty({ description: 'Minimum rating required if rating is mandatory', required: false })
  @IsOptional()
  @IsNumber()
  minimumRating?: number;

  @ApiProperty({ description: 'Feedback form template', required: false })
  @IsOptional()
  @IsJSON()
  feedbackFormTemplate?: object;

  @ApiProperty({ description: 'Certificate expiration in months', required: false })
  @IsOptional()
  @IsNumber()
  expirationMonths?: number;

  @ApiProperty({ enum: VerificationMethod, description: 'Verification method', required: false })
  @IsOptional()
  @IsEnum(VerificationMethod)
  certificateVerificationMethod?: VerificationMethod;

  @ApiProperty({ description: 'Prerequisites', required: false })
  @IsOptional()
  @IsJSON()
  prerequisites?: object;

  @ApiProperty({ description: 'Badge image URL', required: false })
  @IsOptional()
  @IsUrl()
  badgeImage?: string;

  @ApiProperty({ description: 'Signature image URL', required: false })
  @IsOptional()
  @IsUrl()
  signatureImage?: string;

  @ApiProperty({ description: 'Credential title', required: false })
  @IsOptional()
  @IsString()
  credentialTitle?: string;

  @ApiProperty({ description: 'Issuer name', required: false })
  @IsOptional()
  @IsString()
  issuerName?: string;

  @ApiProperty({ description: 'Additional metadata', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;
}
