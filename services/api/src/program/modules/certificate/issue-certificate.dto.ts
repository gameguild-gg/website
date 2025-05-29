import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsOptional, IsJSON, IsEnum, IsDateString } from 'class-validator';
import { CertificateStatus } from '../../entities/enums';

export class IssueCertificateDto {
  @ApiProperty({ description: 'ID of the certificate template to use' })
  @IsString()
  certificateId: string;

  @ApiProperty({ description: 'Program ID for program-specific certificates', required: false })
  @IsOptional()
  @IsString()
  programId?: string;

  @ApiProperty({ description: 'Product ID for product-specific certificates', required: false })
  @IsOptional()
  @IsString()
  productId?: string;

  @ApiProperty({ description: 'ID of the user to issue certificate to' })
  @IsString()
  userId: string;

  @ApiProperty({ description: 'Program user ID if certificate is program-specific', required: false })
  @IsOptional()
  @IsString()
  programUserId?: string;

  @ApiProperty({ enum: CertificateStatus, description: 'Initial status of the certificate', required: false })
  @IsOptional()
  @IsEnum(CertificateStatus)
  status?: CertificateStatus;

  @ApiProperty({ description: 'Certificate expiration date', required: false })
  @IsOptional()
  @IsDateString()
  expiresAt?: Date;

  @ApiProperty({ description: 'Custom metadata for the certificate instance', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;
}
