import { EntityDto } from '@/common/dtos/entity.dto';
import { CreateDateColumn, PrimaryGeneratedColumn, UpdateDateColumn } from 'typeorm';

export abstract class EntityBase implements EntityDto {
  @PrimaryGeneratedColumn('uuid')
  public readonly id: string;

  @CreateDateColumn({ type: 'timestamp' })
  public readonly createdAt: Date;

  @UpdateDateColumn({ type: 'timestamp' })
  public readonly updatedAt: Date;
}
