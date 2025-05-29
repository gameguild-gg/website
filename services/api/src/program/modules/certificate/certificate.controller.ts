import { Controller, Post, Get, Body, Param, UseGuards, HttpStatus, Query } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse, ApiBearerAuth } from '@nestjs/swagger';
import { CertificateService } from './certificate.service';
import { CreateCertificateTemplateDto, IssueCertificateDto, CertificateVerificationResult } from './dtos';
import { AuthGuard, AuthType } from '../../../auth/guards/auth.guard';

@ApiTags('certificates')
@Controller('certificates')
@UseGuards(AuthGuard(AuthType.AccessToken))
@ApiBearerAuth()
export class CertificateController {
  constructor(private readonly certificateService: CertificateService) {}

  @Post('templates')
  @ApiOperation({ summary: 'Create a new certificate template' })
  @ApiResponse({
    status: HttpStatus.CREATED,
    description: 'Certificate template created successfully',
  })
  async createTemplate(@Body() createTemplateDto: CreateCertificateTemplateDto) {
    return this.certificateService.createTemplate(createTemplateDto);
  }

  @Post('issue')
  @ApiOperation({ summary: 'Issue a certificate to a user' })
  @ApiResponse({
    status: HttpStatus.CREATED,
    description: 'Certificate issued successfully',
  })
  async issueCertificate(@Body() issueCertificateDto: IssueCertificateDto) {
    return this.certificateService.issueCertificate(issueCertificateDto);
  }

  @Get('verify/:certificateId')
  @ApiOperation({ summary: 'Verify a certificate by ID' })
  @ApiResponse({
    status: HttpStatus.OK,
    description: 'Certificate verification result',
    type: CertificateVerificationResult,
  })
  async verifyCertificate(@Param('certificateId') certificateId: string): Promise<CertificateVerificationResult> {
    return this.certificateService.verifyCertificate(certificateId);
  }

  @Get('templates')
  @ApiOperation({ summary: 'Get all certificate templates' })
  @ApiResponse({
    status: HttpStatus.OK,
    description: 'List of certificate templates',
  })
  async getTemplates(@Query('programId') programId?: string, @Query('productId') productId?: string) {
    return this.certificateService.listTemplates(programId, productId);
  }

  @Get('user/:userId')
  @ApiOperation({ summary: 'Get certificates for a specific user' })
  @ApiResponse({
    status: HttpStatus.OK,
    description: 'List of user certificates',
  })
  async getUserCertificates(@Param('userId') userId: string) {
    return this.certificateService.getUserCertificates(userId);
  }
}
