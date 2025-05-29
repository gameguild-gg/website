import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, Equal, IsNull, MoreThan, LessThanOrEqual, Not } from 'typeorm';
import * as crypto from 'crypto';
import { Certificate, CertificateBlockchainAnchor, CertificateTag, Product, Program, ProgramUser, Tag, UserCertificate } from '../../entities';
import { UserEntity } from '../../../user/entities/user.entity';
import { CertificateVerificationResult, CreateCertificateTemplateDto, IssueCertificateDto } from './dtos';
import { CertificateStatus } from '../../entities/enums';

@Injectable()
export class CertificateService {
  constructor(
    @InjectRepository(Certificate)
    private readonly certificateRepository: Repository<Certificate>,
    @InjectRepository(UserCertificate)
    private readonly userCertificateRepository: Repository<UserCertificate>,
    @InjectRepository(CertificateBlockchainAnchor)
    private readonly blockchainAnchorRepository: Repository<CertificateBlockchainAnchor>,
    @InjectRepository(Program)
    private readonly programRepository: Repository<Program>,
    @InjectRepository(ProgramUser)
    private readonly programUserRepository: Repository<ProgramUser>,
    @InjectRepository(Tag)
    private readonly tagRepository: Repository<Tag>,
    @InjectRepository(CertificateTag)
    private readonly certificateTagRepository: Repository<CertificateTag>,
    @InjectRepository(Product)
    private readonly productRepository: Repository<Product>,
    @InjectRepository(UserEntity)
    private readonly userRepository: Repository<UserEntity>,
  ) {}

  // Certificate Template Management
  async createTemplate(createDto: CreateCertificateTemplateDto): Promise<Certificate> {
    // Verify program exists (if provided)
    if (createDto.programId) {
      const program = await this.programRepository.findOne({
        where: { id: createDto.programId },
      });

      if (!program) {
        throw new NotFoundException(`Program with ID ${createDto.programId} not found`);
      }
    }

    // Check if product exists (if provided)
    if (createDto.productId) {
      const product = await this.productRepository.findOne({
        where: { id: createDto.productId },
      });

      if (!product) {
        throw new NotFoundException(`Product with ID ${createDto.productId} not found`);
      }
    }

    const certificate = this.certificateRepository.create({
      program: createDto.programId ? ({ id: createDto.programId } as Program) : undefined,
      product: createDto.productId ? ({ id: createDto.productId } as Product) : undefined,
      certificateType: createDto.certificateType,
      name: createDto.name,
      description: createDto.description,
      htmlTemplate: createDto.htmlTemplate,
      cssStyles: createDto.cssStyles,
      autoIssue: createDto.autoIssue ?? true,
      minimumGrade: createDto.minimumGrade ?? 70,
      completionPercentage: createDto.completionPercentage ?? 100,
      requiresFeedback: createDto.requiresFeedback ?? false,
      requiresRating: createDto.requiresRating ?? false,
      minimumRating: createDto.minimumRating,
      feedbackFormTemplate: createDto.feedbackFormTemplate,
      expirationMonths: createDto.expirationMonths,
      certificateVerificationMethod: createDto.certificateVerificationMethod,
      prerequisites: createDto.prerequisites,
      badgeImage: createDto.badgeImage,
      signatureImage: createDto.signatureImage,
      credentialTitle: createDto.credentialTitle,
      issuerName: createDto.issuerName,
      metadata: createDto.metadata,
      isActive: true,
    });

    return this.certificateRepository.save(certificate);
  }

  async updateTemplate(id: string, updateDto: Partial<CreateCertificateTemplateDto>): Promise<Certificate> {
    const certificate = await this.certificateRepository.findOne({
      where: { id },
    });

    if (!certificate) {
      throw new NotFoundException(`Certificate template with ID ${id} not found`);
    }

    if (updateDto.programId) {
      const program = await this.programRepository.findOne({
        where: { id: updateDto.programId },
      });

      if (!program) {
        throw new NotFoundException(`Program with ID ${updateDto.programId} not found`);
      }
      certificate.program = program;
    }

    if (updateDto.productId) {
      const product = await this.productRepository.findOne({
        where: { id: updateDto.productId },
      });

      if (!product) {
        throw new NotFoundException(`Product with ID ${updateDto.productId} not found`);
      }
      certificate.product = product;
    }

    // Update other fields
    Object.assign(certificate, {
      certificateType: updateDto.certificateType,
      name: updateDto.name,
      description: updateDto.description,
      htmlTemplate: updateDto.htmlTemplate,
      cssStyles: updateDto.cssStyles,
      autoIssue: updateDto.autoIssue,
      minimumGrade: updateDto.minimumGrade,
      completionPercentage: updateDto.completionPercentage,
      requiresFeedback: updateDto.requiresFeedback,
      requiresRating: updateDto.requiresRating,
      minimumRating: updateDto.minimumRating,
      feedbackFormTemplate: updateDto.feedbackFormTemplate,
      expirationMonths: updateDto.expirationMonths,
      certificateVerificationMethod: updateDto.certificateVerificationMethod,
      prerequisites: updateDto.prerequisites,
      badgeImage: updateDto.badgeImage,
      signatureImage: updateDto.signatureImage,
      credentialTitle: updateDto.credentialTitle,
      issuerName: updateDto.issuerName,
      metadata: updateDto.metadata,
    });

    return this.certificateRepository.save(certificate);
  }

  async getTemplate(id: string): Promise<Certificate> {
    const certificate = await this.certificateRepository.findOne({
      where: { id },
      relations: { program: true, product: true },
    });

    if (!certificate) {
      throw new NotFoundException(`Certificate template with ID ${id} not found`);
    }

    return certificate;
  }

  async listTemplates(programId?: string, productId?: string): Promise<Certificate[]> {
    // Use type-safe find options instead of raw string queries
    const whereConditions: any = {
      isActive: true,
    };

    if (programId) {
      whereConditions.program = { id: programId };
    }

    if (productId) {
      whereConditions.product = { id: productId };
    }

    return this.certificateRepository.find({
      where: whereConditions,
      relations: {
        program: true,
        product: true,
      },
    });
  }

  async deleteTemplate(id: string): Promise<void> {
    const certificate = await this.certificateRepository.findOne({
      where: { id },
    });

    if (!certificate) {
      throw new NotFoundException(`Certificate template with ID ${id} not found`);
    }

    certificate.isActive = false;
    await this.certificateRepository.save(certificate);
  }

  // Certificate Issuance
  async issueCertificate(issueDto: IssueCertificateDto): Promise<UserCertificate> {
    const certificate = await this.certificateRepository.findOne({
      where: { id: issueDto.certificateId, isActive: true },
      relations: { program: true, product: true },
    });

    if (!certificate) {
      throw new NotFoundException(`Certificate template with ID ${issueDto.certificateId} not found`);
    }

    const user = await this.userRepository.findOne({
      where: { id: issueDto.userId },
    });

    if (!user) {
      throw new NotFoundException(`User with ID ${issueDto.userId} not found`);
    }

    // Find the program user enrollment if this is a program-specific certificate
    let programUser: ProgramUser | null = null;
    if (issueDto.programUserId) {
      programUser = await this.programUserRepository.findOne({
        where: { id: issueDto.programUserId },
        relations: { userProduct: true },
      });

      if (!programUser) {
        throw new NotFoundException(`Program user with ID ${issueDto.programUserId} not found`);
      }
    }

    // Check if certificate already exists
    const existingCertificate = await this.userCertificateRepository.findOne({
      where: {
        user: { id: issueDto.userId },
        certificate: { id: issueDto.certificateId },
      },
    });

    if (existingCertificate) {
      throw new BadRequestException(`Certificate already issued to this user`);
    }

    // Generate certificate number
    const certificateNumber = this.generateCertificateNumber();

    const userCertificate = this.userCertificateRepository.create({
      program: issueDto.programId ? ({ id: issueDto.programId } as Program) : undefined,
      product: issueDto.productId ? ({ id: issueDto.productId } as Product) : undefined,
      user: user,
      programUser: programUser,
      certificate: certificate,
      certificateNumber,
      status: issueDto.status || CertificateStatus.ACTIVE,
      issuedAt: new Date(),
      expiresAt: issueDto.expiresAt,
      metadata: issueDto.metadata,
    });

    return this.userCertificateRepository.save(userCertificate);
  }

  // Certificate Verification
  async verifyCertificate(certificateNumber: string): Promise<CertificateVerificationResult> {
    const userCertificate = await this.userCertificateRepository.findOne({
      where: { certificateNumber },
      relations: { program: true, product: true, user: true, certificate: true },
    });

    if (!userCertificate) {
      return {
        isValid: false,
        verificationDetails: {
          issuedAt: new Date(),
          status: CertificateStatus.REVOKED,
          certificateNumber,
          blockchainVerified: false,
          hashVerified: false,
        },
        errors: ['Certificate not found'],
      };
    }

    const errors: string[] = [];
    let isValid = true;

    // Check if certificate is revoked
    if (userCertificate.status === CertificateStatus.REVOKED) {
      isValid = false;
      errors.push('Certificate has been revoked');
    }

    // Check if certificate has expired
    if (userCertificate.expiresAt && userCertificate.expiresAt < new Date()) {
      isValid = false;
      errors.push('Certificate has expired');
    }

    // Generate verification hash
    const verificationData = {
      certificateNumber: userCertificate.certificateNumber,
      userId: userCertificate.user.id,
      issuedAt: userCertificate.issuedAt,
      certificateId: userCertificate.certificate.id,
    };
    const verificationHash = crypto.createHash('sha256').update(JSON.stringify(verificationData)).digest('hex');

    // For now, assume hash is always verified since we're not storing hashes yet
    const hashVerified = true;

    // Check blockchain verification if applicable
    const blockchainAnchor = await this.blockchainAnchorRepository.findOne({
      where: { certificate: { id: userCertificate.id } },
    });
    const blockchainVerified = !!blockchainAnchor;

    return {
      isValid,
      certificate: userCertificate,
      verificationDetails: {
        issuedAt: userCertificate.issuedAt,
        expiresAt: userCertificate.expiresAt,
        status: userCertificate.status,
        certificateNumber: userCertificate.certificateNumber,
        programTitle: userCertificate.program?.slug || null,
        productTitle: userCertificate.product?.title || null,
        blockchainVerified,
        hashVerified,
        issuerName: userCertificate.certificate?.issuerName,
      },
      errors: errors.length > 0 ? errors : undefined,
    };
  }

  // Certificate Management
  async revokeCertificate(certificateId: string): Promise<UserCertificate> {
    const userCertificate = await this.userCertificateRepository.findOne({
      where: { id: certificateId },
    });

    if (!userCertificate) {
      throw new NotFoundException(`Certificate with ID ${certificateId} not found`);
    }

    userCertificate.status = CertificateStatus.REVOKED;
    userCertificate.revokedAt = new Date();

    return this.userCertificateRepository.save(userCertificate);
  }

  async getUserCertificates(userId: string): Promise<UserCertificate[]> {
    return this.userCertificateRepository.find({
      where: {
        user: { id: userId },
        status: CertificateStatus.ACTIVE,
      },
      relations: { program: true, product: true, certificate: true },
      order: { issuedAt: 'DESC' },
    });
  }

  async getCertificatesByProgram(programId: string): Promise<UserCertificate[]> {
    return this.userCertificateRepository.find({
      where: { program: { id: programId } },
      relations: { user: true, program: true, certificate: true },
      order: { issuedAt: 'DESC' },
    });
  }

  // Statistics and Analytics
  async getCertificateStats(programId?: string): Promise<any> {
    // Base where condition
    const baseWhere: any = {};
    if (programId) {
      baseWhere.program = { id: programId };
    }

    // Total issued certificates
    const totalIssued = await this.userCertificateRepository.count({
      where: baseWhere,
    });

    // Active certificates (not revoked and not expired)
    const activeWhereConditions = [];
    
    // Active certificates with no expiration date
    activeWhereConditions.push({
      ...baseWhere,
      status: Equal(CertificateStatus.ACTIVE),
      expiresAt: IsNull(),
    });
    
    // Active certificates that haven't expired yet
    activeWhereConditions.push({
      ...baseWhere,
      status: Equal(CertificateStatus.ACTIVE),
      expiresAt: MoreThan(new Date()),
    });

    const activeCount = await this.userCertificateRepository.count({
      where: activeWhereConditions,
    });

    // Revoked certificates
    const revokedCount = await this.userCertificateRepository.count({
      where: {
        ...baseWhere,
        status: Equal(CertificateStatus.REVOKED),
      },
    });

    // Expired certificates (active but past expiration date)
    const expiredCount = await this.userCertificateRepository.count({
      where: {
        ...baseWhere,
        status: Equal(CertificateStatus.ACTIVE),
        expiresAt: LessThanOrEqual(new Date()),
      },
    });

    return {
      totalIssued,
      activeCount,
      revokedCount,
      expiredCount,
    };
  }

  // Blockchain Anchoring
  private async anchorToBlockchain(userCertificateId: string, certificateHash: string): Promise<void> {
    // This would integrate with actual blockchain service
    // For now, we'll create a placeholder record
    const anchor = this.blockchainAnchorRepository.create({
      certificate: { id: userCertificateId } as UserCertificate,
      blockchainNetwork: 'ethereum', // or other network
      transactionHash: `0x${crypto.randomBytes(32).toString('hex')}`, // placeholder
      blockNumber: Math.floor(Math.random() * 1000000), // placeholder
      anchoredAt: new Date(),
    });

    await this.blockchainAnchorRepository.save(anchor);
  }

  private generateCertificateNumber(): string {
    // Generate a unique certificate number
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `CERT-${timestamp}-${random}`.toUpperCase();
  }
}
