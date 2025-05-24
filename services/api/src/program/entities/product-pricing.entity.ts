import { Entity, Column, ManyToOne, JoinColumn, OneToMany, DeleteDateColumn } from 'typeorm';
import { IsNumber, IsOptional } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { Product } from './product.entity';
import { FinancialTransaction } from './financial-transaction.entity';

@Entity('product_pricing')
export class ProductPricing extends EntityBase {
  @ApiProperty({ description: 'Standard price of the product before any discounts' })
  @Column('decimal', { precision: 10, scale: 2 })
  @IsNumber()
  basePrice: number;

  @ApiProperty({ description: 'Percentage of revenue that goes to the content creator', default: 70 })
  @Column('decimal', { precision: 5, scale: 2, default: 70 })
  @IsNumber()
  creatorSharePercentage: number;

  @ApiProperty({ description: 'Applicable tax rate for this product' })
  @Column('decimal', { precision: 5, scale: 4 })
  @IsNumber()
  taxRate: number;

  @ApiProperty({ description: 'Rules for when this pricing is available (e.g., time-limited offers, regional pricing)', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  availabilityRules?: Record<string, any>;

  @ApiProperty({ description: 'Soft delete timestamp - when the pricing was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ApiProperty({ type: () => Product, description: 'The product this pricing applies to' })
  @ManyToOne(() => Product, (product) => product.pricing, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  product: Product;

  @ApiProperty({ type: () => FinancialTransaction, isArray: true, description: 'Financial transactions associated with this pricing' })
  @OneToMany(() => FinancialTransaction, (transaction) => transaction.pricing)
  transactions: FinancialTransaction[];
}
