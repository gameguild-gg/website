import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { UserKycVerification } from '../program/entities';

import { KycController } from './kyc.controller';
import { KycService } from './kyc.service';
import { KycVerificationService } from './kyc-verification.service';

import { UserModule } from '../user/user.module';

@Module({
  imports: [TypeOrmModule.forFeature([UserKycVerification]), forwardRef(() => UserModule)],
  controllers: [KycController],
  providers: [KycService, KycVerificationService],
  exports: [KycService, KycVerificationService],
})
export class KycModule {}
