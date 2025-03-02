import { Column, Entity } from 'typeorm';
import { EntityBase } from '@/common/entities/entity.base';

@Entity('languages')
export class LanguageEntity extends EntityBase {
  @Column({ type: 'varchar', length: 64, nullable: false, unique: true })
  code: string; // Ex.: 'pt-BR', 'en-US', etc.

  @Column({ type: 'varchar', length: 64, nullable: false })
  name: string;
}
