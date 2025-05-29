import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import {
  Certificate,
  UserCertificate,
  CertificateBlockchainAnchor,
  CertificateTag,
  ProgramFeedbackSubmission,
  ProgramRatingEntity,
  Program,
  Product,
  ProgramUser,
  Tag,
} from '../../entities';

import { CertificateController } from './certificate.controller';
import { CertificateService } from './certificate.service';
import { CertificateVerificationService } from './certificate-verification.service';
import { CertificateGenerationService } from './certificate-generation.service';

// Import UserEntity from user module
import { UserEntity } from '../../../user/entities/user.entity';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      Certificate,
      UserCertificate,
      CertificateBlockchainAnchor,
      CertificateTag,
      ProgramFeedbackSubmission,
      ProgramRatingEntity,
      Program,
      Product,
      ProgramUser,
      Tag,
      UserEntity,
    ]),
  ],
  controllers: [CertificateController],
  providers: [CertificateService, CertificateVerificationService, CertificateGenerationService],
  exports: [CertificateService, CertificateVerificationService, CertificateGenerationService],
})
export class CertificateModule {}
