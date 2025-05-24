import { Entity, Column, ManyToOne, JoinColumn, Index, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsNumber, IsOptional, IsEnum, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities/user.entity';
import { Product } from './product.entity';
import { ProductPricing } from './product-pricing.entity';
import { ProductSubscriptionPlan } from './product-subscription-plan.entity';
import { PromoCode } from './promo-code.entity';
import { TransactionType, TransactionStatus } from './enums';

@Entity('financial_transactions')
@Index((entity) => [entity.fromUser])
@Index((entity) => [entity.toUser])
@Index((entity) => [entity.product])
@Index((entity) => [entity.transactionType])
@Index((entity) => [entity.status])
@Index((entity) => [entity.paymentProviderTransactionId])
@Index((entity) => [entity.createdAt])
@Index((entity) => [entity.referrerUser])
export class FinancialTransaction extends EntityBase {
  @ApiProperty({ type: () => UserEntity, description: 'Source user, null if system credit or external payment', required: false })
  @ManyToOne(() => UserEntity, { nullable: true })
  @JoinColumn({ name: 'from_user_id' })
  @IsOptional()
  fromUser?: UserEntity;

  @ApiProperty({ type: () => UserEntity, description: 'Destination user, null if system debit or external payment', required: false })
  @ManyToOne(() => UserEntity, { nullable: true })
  @JoinColumn({ name: 'to_user_id' })
  @IsOptional()
  toUser?: UserEntity;

  @ApiProperty({ type: () => Product, description: 'Product being purchased, if applicable', required: false })
  @ManyToOne(() => Product, { nullable: true })
  @JoinColumn({ name: 'product_id' })
  @IsOptional()
  product?: Product;

  @ApiProperty({ type: () => ProductPricing, description: 'Specific pricing used for this transaction', required: false })
  @ManyToOne(() => ProductPricing, { nullable: true })
  @JoinColumn({ name: 'pricing_id' })
  @IsOptional()
  pricing?: ProductPricing;

  @ApiProperty({ type: () => ProductSubscriptionPlan, description: 'Subscription plan for subscription transactions', required: false })
  @ManyToOne(() => ProductSubscriptionPlan, { nullable: true })
  @JoinColumn({ name: 'subscription_plan_id' })
  @IsOptional()
  subscriptionPlan?: ProductSubscriptionPlan;

  @ApiProperty({ type: () => PromoCode, description: 'Promo code applied to this transaction', required: false })
  @ManyToOne(() => PromoCode, { nullable: true })
  @JoinColumn({ name: 'promo_code_id' })
  @IsOptional()
  promoCode?: PromoCode;

  @Column({ type: 'enum', enum: TransactionType })
  @ApiProperty({ enum: TransactionType, description: 'Type of financial transaction (purchase, refund, etc.)' })
  @IsEnum(TransactionType)
  transactionType: TransactionType;

  @Column('decimal', { precision: 10, scale: 2 })
  @ApiProperty({ description: 'Actual amount of the transaction after discounts' })
  @IsNumber()
  amount: number;

  @Column('decimal', { precision: 10, scale: 2 })
  @ApiProperty({ description: 'Original amount before any discounts or adjustments' })
  @IsNumber()
  originalAmount: number;

  @ManyToOne(() => UserEntity, { nullable: true })
  @JoinColumn({ name: 'referrer_user_id' })
  @ApiProperty({ description: 'User who referred this transaction for commission', required: false })
  @IsOptional()
  referrerUser?: UserEntity;

  @Column('decimal', { precision: 10, scale: 2, nullable: true })
  @ApiProperty({ description: 'Amount paid to referrer as commission', required: false })
  @IsOptional()
  @IsNumber()
  referralCommissionAmount?: number;

  @Column({ type: 'enum', enum: TransactionStatus })
  @ApiProperty({ enum: TransactionStatus, description: 'Current status of this transaction (pending, completed, etc.)' })
  @IsEnum(TransactionStatus)
  status: TransactionStatus;

  @Column('varchar', { length: 255 })
  @ApiProperty({ description: 'Payment processor used (stripe, paypal, crypto, etc.)' })
  @IsString()
  @IsNotEmpty()
  paymentProvider: string;

  @Column('varchar', { length: 255 })
  @ApiProperty({ description: 'External transaction ID from the payment provider for reconciliation' })
  @IsString()
  @IsNotEmpty()
  paymentProviderTransactionId: string;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Additional transaction-specific data such as payment details, items, etc.', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the transaction was deleted, null if active', required: false })
  deletedAt: Date | null;
}
