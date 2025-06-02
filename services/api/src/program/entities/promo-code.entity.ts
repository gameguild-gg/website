import { Entity, Column, ManyToOne, OneToMany, JoinColumn, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsNumber, IsOptional, IsEnum, IsDateString, IsBoolean } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { PromoCodeType } from './enums';
import { Product } from './product.entity';
import { UserEntity } from '../../user/entities/user.entity';
import { FinancialTransaction } from './financial-transaction.entity';

@Entity('promo_codes')
export class PromoCode extends EntityBase {
  @ApiProperty({ description: 'The code users enter to apply the discount' })
  @Column('varchar', { length: 50, unique: true })
  @IsString()
  @IsNotEmpty()
  code: string;

  @ApiProperty({ description: 'What kind of discount this code provides' })
  @Column({ type: 'enum', enum: PromoCodeType })
  @IsEnum(PromoCodeType)
  type: PromoCodeType;

  @ApiProperty({ description: 'Percentage discount when type is percentage_off', required: false })
  @Column('decimal', { precision: 5, scale: 2, nullable: true })
  @IsNumber()
  @IsOptional()
  discountPercentage?: number;

  @ApiProperty({ description: 'How much of discount is paid by affiliate', required: false })
  @Column('decimal', { precision: 5, scale: 2, nullable: true })
  @IsNumber()
  @IsOptional()
  affiliatePercentage?: number;

  @ApiProperty({ description: 'Null for unlimited uses', required: false })
  @Column('integer', { nullable: true })
  @IsNumber()
  @IsOptional()
  maxUses?: number;

  @ApiProperty({ description: 'Current number of times this code has been used', default: 0 })
  @Column('integer', { default: 0 })
  @IsNumber()
  usesCount: number;

  @ApiProperty({ description: 'How many times one user can use this code', default: 1 })
  @Column('integer', { default: 1 })
  @IsNumber()
  maxUsesPerUser: number;

  @ApiProperty({ description: 'When this code becomes valid' })
  @Column('timestamp', { default: () => 'now()' })
  @IsDateString()
  startsAt: Date;

  @ApiProperty({ description: 'When this code expires, null for no expiration', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  expiresAt?: Date;

  @ApiProperty({ description: 'Whether this code can currently be used', default: true })
  @Column('boolean', { default: true })
  @IsBoolean()
  isActive: boolean;

  @ApiProperty({ description: 'Soft delete timestamp - when the code was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ManyToOne(() => Product, { nullable: true })
  @JoinColumn({ name: 'product_id' })
  product?: Product;

  @ManyToOne(() => UserEntity)
  @JoinColumn({ name: 'affiliate_user_id' })
  affiliateUser?: UserEntity;

  @ManyToOne(() => UserEntity)
  @JoinColumn({ name: 'created_by' })
  createdBy: UserEntity;

  @OneToMany(() => FinancialTransaction, (transaction) => transaction.promoCode)
  transactions: FinancialTransaction[];
}
