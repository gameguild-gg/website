import { Entity, Column, ManyToOne, JoinColumn, OneToMany, Index, DeleteDateColumn } from 'typeorm';
import { IsOptional, IsEnum, IsDateString, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { ProductAcquisitionType, ProductAccessStatus } from './enums';
import { UserEntity } from '../../user/entities/user.entity';
import { Product } from './product.entity';
import { ProgramUser } from './program-user.entity';
import { UserSubscription } from './user-subscription.entity';
import { FinancialTransaction } from './financial-transaction.entity';

@Entity('user_products')
@Index((entity) => [entity.user, entity.product])
@Index((entity) => [entity.subscription])
@Index((entity) => [entity.status])
@Index((entity) => [entity.accessExpiresAt])
@Index((entity) => [entity.acquisitionType])
export class UserProduct extends EntityBase {
  @ApiProperty({ type: () => UserEntity, description: 'User who has access to this product' })
  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  user: UserEntity;

  @ApiProperty({ type: () => Product, description: 'Product the user has access to' })
  @ManyToOne(() => Product, (product) => product.userProducts, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  product: Product;

  @ApiProperty({ type: () => UserSubscription, description: 'If product access is through subscription', required: false })
  @ManyToOne(() => UserSubscription, { nullable: true })
  @JoinColumn({ name: 'subscription_id' })
  @IsOptional()
  subscription?: UserSubscription;

  @Column({ type: 'enum', enum: ProductAcquisitionType })
  @IsEnum(ProductAcquisitionType)
  @ApiProperty({ description: 'How the product was acquired (purchased, subscription, gift, etc.)' })
  acquisitionType: ProductAcquisitionType;

  @ApiProperty({ type: () => FinancialTransaction, description: 'Reference to the transaction if purchased', required: false })
  @ManyToOne(() => FinancialTransaction, { nullable: true })
  @JoinColumn({ name: 'purchase_transaction_id' })
  @IsOptional()
  purchaseTransaction?: FinancialTransaction;

  @Column('timestamp', { nullable: true })
  @IsOptional()
  @IsDateString()
  @ApiProperty({ description: 'Null means permanent access, otherwise when access ends', required: false })
  accessExpiresAt?: Date;

  @Column('timestamp', { default: () => 'CURRENT_TIMESTAMP' })
  @IsDateString()
  @ApiProperty({ description: 'When the user first gained access' })
  accessStartedAt: Date;

  @Column({ type: 'enum', enum: ProductAccessStatus, default: ProductAccessStatus.ACTIVE })
  @IsEnum(ProductAccessStatus)
  @ApiProperty({ description: 'Current status of user access to this product' })
  status: ProductAccessStatus;

  @Column('jsonb')
  @IsJSON()
  @ApiProperty({ description: 'Flexible storage for product-specific access settings, usage limitations, custom permissions, etc.' })
  metadata: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the access record was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @ApiProperty({ type: () => ProgramUser, isArray: true, description: 'Program user entries associated with this product access' })
  @OneToMany(() => ProgramUser, (programUser) => programUser.userProduct)
  programUsers: ProgramUser[];
}
