import { Column, DeleteDateColumn, Entity, Index, OneToMany } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';

import { IsBoolean, IsEnum, IsJSON, IsNotEmpty, IsNumber, IsOptional, IsString } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ProductType, Visibility } from './enums';
import { ProductProgram } from './product-program.entity';
import { ProductPricing } from './product-pricing.entity';
import { ProductSubscriptionPlan } from './product-subscription-plan.entity';
import { UserProduct } from './user-product.entity';

@Entity('products')
@Index((entity) => [entity.type])
@Index((entity) => [entity.isBundle])
@Index((entity) => [entity.visibility])
@Index((entity) => [entity.createdAt])
export class Product extends EntityBase {
  @Column({ type: 'varchar', length: 255 })
  @ApiProperty({ description: 'Product title/name displayed to users' })
  @IsNotEmpty()
  @IsString()
  title: string;

  @Column({ type: 'text' })
  @ApiProperty({ description: 'Detailed description of the product' })
  @IsString()
  description: string;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'URL or path to product thumbnail image', required: false })
  @IsOptional()
  @IsString()
  thumbnail: string | null;

  @Column({ type: 'enum', enum: ProductType, default: ProductType.PROGRAM })
  @ApiProperty({ enum: ProductType, description: 'Type of product (program, bundle, subscription, etc.)' })
  @IsEnum(ProductType)
  type: ProductType;

  @Column({ type: 'boolean', default: false })
  @ApiProperty({ description: 'Whether this product is a bundle of other products' })
  @IsBoolean()
  isBundle: boolean;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Array of product IDs included in the bundle', required: false })
  @IsOptional()
  @IsJSON()
  bundleItems: object | null;

  @Column({ type: 'jsonb' })
  @ApiProperty({ description: 'Flexible storage for product type-specific configuration' })
  @IsJSON()
  metadata: object;

  @Column({ type: 'decimal', precision: 5, scale: 2, default: 30.0 })
  @ApiProperty({ description: 'Default 30% commission for referrals' })
  @IsNumber()
  referralCommissionPercentage: number;

  @Column({ type: 'decimal', precision: 5, scale: 2, default: 0.0 })
  @ApiProperty({ description: 'Maximum % discount affiliate can offer, 0 means no affiliate allowed' })
  @IsNumber()
  maxAffiliateDiscount: number;

  @Column({ type: 'decimal', precision: 5, scale: 2, default: 30.0 })
  @ApiProperty({ description: 'Commission % from remaining value after discount' })
  @IsNumber()
  affiliateCommissionPercentage: number;

  @Column({ type: 'enum', enum: Visibility, default: Visibility.DRAFT })
  @ApiProperty({ enum: Visibility, description: 'Product visibility status (draft, published, archived)' })
  @IsEnum(Visibility)
  visibility: Visibility;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the product was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @OneToMany(() => ProductProgram, (productProgram) => productProgram.product)
  @ApiProperty({ type: () => ProductProgram, isArray: true, description: 'Program relationships for this product' })
  productPrograms: ProductProgram[];

  @OneToMany(() => ProductPricing, (productPricing) => productPricing.product)
  @ApiProperty({ type: () => ProductPricing, isArray: true, description: 'Pricing options for this product' })
  pricing: ProductPricing[];

  @OneToMany(() => ProductSubscriptionPlan, (subscriptionPlan) => subscriptionPlan.product)
  @ApiProperty({
    type: () => ProductSubscriptionPlan,
    isArray: true,
    description: 'Subscription plans for this product',
  })
  subscriptionPlans: ProductSubscriptionPlan[];

  @OneToMany(() => UserProduct, (userProduct) => userProduct.product)
  @ApiProperty({ type: () => UserProduct, isArray: true, description: 'Users who have access to this product' })
  userProducts: UserProduct[];
}
