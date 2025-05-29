import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import {
  FinancialTransaction,
  UserFinancialMethod,
  UserSubscription,
  ProductPricing,
  ProductSubscriptionPlan,
  PromoCode,
  PromoCodeUse,
  UserProduct,
} from '../program/entities';

import { FinancialController } from './financial.controller';
import { FinancialService } from './financial.service';
import { SubscriptionService } from './subscription.service';
import { PaymentService } from './payment.service';

import { UserModule } from '../user/user.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      FinancialTransaction,
      UserFinancialMethod,
      UserSubscription,
      ProductPricing,
      ProductSubscriptionPlan,
      PromoCode,
      PromoCodeUse,
      UserProduct,
    ]),
    forwardRef(() => UserModule),
  ],
  controllers: [FinancialController],
  providers: [FinancialService, SubscriptionService, PaymentService],
  exports: [FinancialService, SubscriptionService, PaymentService],
})
export class FinancialModule {}
