import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

// Core entities
import {
  Program,
  ProgramContent,
  ProgramUser,
  ProgramUserRole,
  ContentInteraction,
  ActivityGrade,
  Product,
  ProductProgram,
  ProgramRatingEntity,
  ProgramFeedbackSubmission,
  UserProduct,
} from './entities';

// Sub-modules
import { ProgramContentModule } from './modules/program-content/program-content.module';
import { ProgramEnrollmentModule } from './modules/program-enrollment/program-enrollment.module';
import { ProgramProgressModule } from './modules/program-progress/program-progress.module';
import { ProgramAnalyticsModule } from './modules/program-analytics/program-analytics.module';
import { ProgramGradingModule } from './modules/program-grading/program-grading.module';
import { CertificateModule } from './modules/certificate/certificate.module';
import { TagModule } from './modules/tag/tag.module';

// Controllers
import { ProgramController } from './controllers/program.controller';

// Services
import { ProgramService } from './services/program.service';

// External modules
import { UserModule } from '../user/user.module';
import { AuthModule } from '../auth/auth.module';
import { ProductModule } from '../product/product.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      Program,
      ProgramContent,
      ProgramUser,
      ProgramUserRole,
      ContentInteraction,
      ActivityGrade,
      Product,
      ProductProgram,
      ProgramRatingEntity,
      ProgramFeedbackSubmission,
      UserProduct,
    ]),
    // Sub-modules for specialized functionality
    ProgramContentModule,
    ProgramEnrollmentModule,
    ProgramProgressModule,
    ProgramAnalyticsModule,
    ProgramGradingModule,
    CertificateModule,
    TagModule,

    // External modules with circular dependency handling
    forwardRef(() => UserModule),
    forwardRef(() => AuthModule),
    forwardRef(() => ProductModule),
  ],
  controllers: [ProgramController],
  providers: [ProgramService],
  exports: [
    ProgramService,
    ProgramContentModule,
    ProgramEnrollmentModule,
    ProgramProgressModule,
    ProgramAnalyticsModule,
    ProgramGradingModule,
    CertificateModule,
    TagModule,
  ],
})
export class ProgramModule {}
