import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsBoolean, IsDate, IsEnum, IsOptional, IsString } from 'class-validator';
import { Type } from 'class-transformer';
import { UserCertificate } from '../../entities';
import { CertificateStatus } from '../../entities/enums';

// Move VerificationDetails above CertificateVerificationResult
export class VerificationDetails {
  @ApiProperty({ description: 'Date when certificate was issued' })
  @IsDate()
  @Type(() => Date)
  issuedAt: Date;

  @ApiProperty({ description: 'Certificate expiration date', required: false })
  @IsOptional()
  @IsDate()
  @Type(() => Date)
  expiresAt?: Date;

  @ApiProperty({ description: 'Certificate status' })
  @IsEnum(CertificateStatus)
  status: CertificateStatus;

  @ApiProperty({ description: 'Certificate number for verification' })
  @IsString()
  certificateNumber: string;

  @ApiProperty({ description: 'Program title if applicable', required: false })
  @IsOptional()
  @IsString()
  programTitle?: string;

  @ApiProperty({ description: 'Product title if applicable', required: false })
  @IsOptional()
  @IsString()
  productTitle?: string;

  @ApiProperty({ description: 'Whether blockchain verification was successful' })
  @IsBoolean()
  blockchainVerified: boolean;

  @ApiProperty({ description: 'Whether hash verification was successful' })
  @IsBoolean()
  hashVerified: boolean;

  @ApiProperty({ description: 'Issuer name', required: false })
  @IsOptional()
  @IsString()
  issuerName?: string;
}

export class CertificateVerificationResult {
  @ApiProperty({ description: 'Whether the certificate is valid' })
  @IsBoolean()
  isValid: boolean;

  @ApiProperty({ description: 'Certificate details if valid', required: false })
  @IsOptional()
  certificate?: UserCertificate;

  @ApiProperty({ description: 'Verification details' })
  @Type(() => VerificationDetails)
  verificationDetails: VerificationDetails;

  @ApiProperty({ description: 'Validation errors if any', type: [String], required: false })
  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  errors?: string[];
}
