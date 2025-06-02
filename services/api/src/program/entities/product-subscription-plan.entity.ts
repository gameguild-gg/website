import { Entity, Column, ManyToOne, JoinColumn, OneToMany, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsNumber, IsOptional, IsEnum } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { SubscriptionType, SubscriptionBillingInterval } from './enums';
import { Product } from './product.entity';
import { UserSubscription } from './user-subscription.entity';

@Entity('product_subscription_plans')
export class ProductSubscriptionPlan extends EntityBase {
  @ApiProperty({ description: 'Display name of the subscription plan' })
  @Column('varchar', { length: 255 })
  @IsString()
  @IsNotEmpty()
  name: string;

  @ApiProperty({ description: 'Detailed description of the subscription plan benefits', required: false })
  @Column('text', { nullable: true })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty({ description: 'Type of subscription (monthly, quarterly, annual, etc.)' })
  @Column({ type: 'enum', enum: SubscriptionType })
  @IsEnum(SubscriptionType)
  type: SubscriptionType;

  @ApiProperty({ description: 'Current price of the subscription, stored as decimal for precision' })
  @Column('decimal', { precision: 10, scale: 2 })
  @IsNumber()
  price: number;

  @ApiProperty({ description: 'Original price before any discounts, used for comparison' })
  @Column('decimal', { precision: 10, scale: 2 })
  @IsNumber()
  basePrice: number;

  @ApiProperty({ description: 'Time unit for billing (day, week, month, year)' })
  @Column({ type: 'enum', enum: SubscriptionBillingInterval })
  @IsEnum(SubscriptionBillingInterval)
  billingInterval: SubscriptionBillingInterval;

  @ApiProperty({ description: 'Number of intervals between billings, e.g. 3 for every 3 months', default: 1 })
  @Column('integer', { default: 1 })
  @IsNumber()
  billingIntervalCount: number;

  @ApiProperty({ description: 'Number of days in free trial period, null for no trial', required: false })
  @Column('integer', { nullable: true })
  @IsNumber()
  @IsOptional()
  trialPeriodDays?: number;

  @ApiProperty({ description: 'Array of features included in this subscription plan', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  features?: Record<string, any>;

  @ApiProperty({ description: 'Rules for when this subscription plan is available (region, user type, etc.)', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  availabilityRules?: Record<string, any>;

  @ApiProperty({ description: 'Soft delete timestamp - when the plan was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ManyToOne(() => Product, (product) => product.subscriptionPlans, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  product: Product;

  @OneToMany(() => UserSubscription, (subscription) => subscription.subscriptionPlan)
  subscriptions: UserSubscription[];
}
