import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { Product, ProductProgram, ProductPricing, ProductSubscriptionPlan, UserProduct, PromoCode, PromoCodeUse } from '../program/entities';

import { ProductController } from './product.controller';
import { ProductService } from './product.service';

// Circular dependency with ProgramModule
import { ProgramModule } from '../program/program.module';
import { UserModule } from '../user/user.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([Product, ProductProgram, ProductPricing, ProductSubscriptionPlan, UserProduct, PromoCode, PromoCodeUse]),
    forwardRef(() => ProgramModule),
    forwardRef(() => UserModule),
  ],
  controllers: [ProductController],
  providers: [ProductService],
  exports: [ProductService],
})
export class ProductModule {}
