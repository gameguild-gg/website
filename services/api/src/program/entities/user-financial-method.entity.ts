import { Entity, Column, ManyToOne, Index } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsEnum, IsBoolean, IsNumber } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { PaymentMethodType, PaymentMethodStatus } from './enums';
import { UserEntity } from '../../user/entities/user.entity';

@Entity('user_financial_methods')
@Index((method) => [method.user, method.status])
export class UserFinancialMethod extends EntityBase {
  @Column('uuid')
  @IsString()
  @IsNotEmpty()
  userId: string;

  @Column({ type: 'enum', enum: PaymentMethodType })
  @IsEnum(PaymentMethodType)
  methodType: PaymentMethodType;

  @Column('varchar', { length: 255, nullable: true })
  @IsString()
  @IsOptional()
  providerMethodId?: string;

  @Column('varchar', { length: 100, nullable: true })
  @IsString()
  @IsOptional()
  lastFourDigits?: string;

  @Column('varchar', { length: 100, nullable: true })
  @IsString()
  @IsOptional()
  brand?: string;

  @Column('integer', { nullable: true })
  @IsNumber()
  @IsOptional()
  expiryMonth?: number;

  @Column('integer', { nullable: true })
  @IsNumber()
  @IsOptional()
  expiryYear?: number;

  @Column('boolean', { default: true })
  @IsBoolean()
  isActive: boolean;

  @Column('boolean', { default: false })
  @IsBoolean()
  isDefault: boolean;

  @Column({ type: 'enum', enum: PaymentMethodStatus, default: PaymentMethodStatus.ACTIVE })
  @IsEnum(PaymentMethodStatus)
  status: PaymentMethodStatus;

  @Column('jsonb', { nullable: true })
  @IsOptional()
  metadata?: object;

  // Relations
  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  user: UserEntity;
}
