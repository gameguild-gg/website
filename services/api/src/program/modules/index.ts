// Main module
export { ProgramModule } from '../program.module';

// Sub-modules
export { ProgramContentModule } from './program-content/program-content.module';
export { ProgramEnrollmentModule } from './program-enrollment/program-enrollment.module';
export { ProgramProgressModule } from './program-progress/program-progress.module';
export { ProgramAnalyticsModule } from './program-analytics/program-analytics.module';
export { ProgramGradingModule } from './program-grading/program-grading.module';
export { CertificateModule } from './certificate/certificate.module';
export { TagModule } from './tag/tag.module';

// Related modules
export { ProductModule } from '../../product/product.module';

// Independent modules (moved to top-level)
// KycModule is now at src/kyc/kyc.module.ts
// FinancialModule is now at src/financial/financial.module.ts
