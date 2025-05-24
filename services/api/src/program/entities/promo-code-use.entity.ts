import { Entity, Column, ManyToOne, Index } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsNumber, IsDate, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';
import { PromoCode } from './promo-code.entity';
import { UserEntity } from '../../user/entities/user.entity';
import { FinancialTransaction } from './financial-transaction.entity';

@Entity('promo_code_uses')
@Index((entity) => [entity.promoCode])
@Index((entity) => [entity.user])
@Index((entity) => [entity.financialTransaction])
@Index((entity) => [entity.usedAt])
export class PromoCodeUse extends EntityBase {
  @ApiProperty({ type: () => PromoCode, description: 'Reference to the promo code that was used' })
  @ManyToOne(() => PromoCode, { nullable: false })
  @ValidateNested()
  @Type(() => PromoCode)
  promoCode: PromoCode;

  @ApiProperty({ type: () => UserEntity, description: 'User who used the promo code' })
  @ManyToOne(() => UserEntity, { nullable: false })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  @ApiProperty({ type: () => FinancialTransaction, description: 'Transaction where the code was applied' })
  @ManyToOne(() => FinancialTransaction, { nullable: false })
  @ValidateNested()
  @Type(() => FinancialTransaction)
  financialTransaction: FinancialTransaction;

  @Column({ type: 'decimal', precision: 10, scale: 2 })
  @ApiProperty({ description: 'Actual amount discounted from the transaction' })
  @IsNotEmpty()
  @IsNumber()
  discountApplied: number;

  @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP' })
  @ApiProperty({ description: 'When the promo code was used' })
  @IsNotEmpty()
  @IsDate()
  usedAt: Date;
}
