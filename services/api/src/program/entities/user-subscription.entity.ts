import { Entity, Column, ManyToOne, JoinColumn, DeleteDateColumn } from 'typeorm';
import { IsOptional, IsEnum, IsDateString, IsBoolean } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { SubscriptionStatus } from './enums';
import { UserEntity } from '../../user/entities/user.entity';
import { ProductSubscriptionPlan } from './product-subscription-plan.entity';

@Entity('user_subscriptions')
export class UserSubscription extends EntityBase {
  @ApiProperty({ description: 'Current status of the subscription (active, canceled, etc.)' })
  @Column({ type: 'enum', enum: SubscriptionStatus })
  @IsEnum(SubscriptionStatus)
  status: SubscriptionStatus;

  @ApiProperty({ description: 'Start date of the current billing period' })
  @Column('timestamp')
  @IsDateString()
  currentPeriodStart: Date;

  @ApiProperty({ description: 'End date of the current billing period' })
  @Column('timestamp')
  @IsDateString()
  currentPeriodEnd: Date;

  @ApiProperty({ description: 'Whether subscription will cancel at the end of current period', default: false })
  @Column('boolean', { default: false })
  @IsBoolean()
  cancelAtPeriodEnd: boolean;

  @ApiProperty({ description: 'When the subscription was canceled, null if not canceled', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  canceledAt?: Date;

  @ApiProperty({ description: 'When the subscription ended, null if still active', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  endedAt?: Date;

  @ApiProperty({ description: 'When the trial period ends, null if no trial or trial ended', required: false })
  @Column('timestamp', { nullable: true })
  @IsDateString()
  @IsOptional()
  trialEnd?: Date;

  @ApiProperty({ description: 'Soft delete timestamp - when the subscription was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ApiProperty({ type: () => UserEntity, description: 'User who owns this subscription' })
  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  user: UserEntity;

  @ApiProperty({ type: () => ProductSubscriptionPlan, description: 'The subscription plan this user is enrolled in' })
  @ManyToOne(() => ProductSubscriptionPlan, (plan) => plan.subscriptions)
  @JoinColumn({ name: 'subscription_plan_id' })
  subscriptionPlan: ProductSubscriptionPlan;
}
