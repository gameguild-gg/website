import { Entity, Column, ManyToOne, JoinColumn, DeleteDateColumn } from 'typeorm';
import { IsNumber, IsBoolean } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { Product } from './product.entity';
import { Program } from './program.entity';

@Entity('product_programs')
export class ProductProgram extends EntityBase {
  @ApiProperty({ description: 'Position/order in the product/program sequence' })
  @Column('float')
  @IsNumber()
  orderIndex: number;

  @ApiProperty({ description: 'Whether this is the primary product for this program', default: false })
  @Column('boolean', { default: false })
  @IsBoolean()
  isPrimary: boolean;

  @ApiProperty({ description: 'Soft delete timestamp - when the relationship was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ApiProperty({ type: () => Product, description: 'The product associated with this program' })
  @ManyToOne(() => Product, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'product_id' })
  product: Product;

  @ApiProperty({ type: () => Program, description: 'The program associated with this product' })
  @ManyToOne(() => Program, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  program: Program;
}
