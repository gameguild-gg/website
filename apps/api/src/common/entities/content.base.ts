import { EntityBase } from '@/common/entities/entity.base';
import { ContentDto } from '@/common/dtos/content.dto';
import { Column, Index } from 'typeorm';

export abstract class ContentBase extends EntityBase implements ContentDto {
  @Column({ type: 'varchar', length: 255, nullable: false })
  @Index({ unique: true })
  public readonly slug: string;
}
