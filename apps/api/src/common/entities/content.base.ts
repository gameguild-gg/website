import { Column, Index } from 'typeorm';

import { ContentDto } from '@/common/dtos/content.dto';
import { EntityBase } from '@/common/entities/entity.base';

export abstract class ContentBase extends EntityBase implements ContentDto {
  @Column({ type: 'varchar', length: 255, nullable: false })
  @Index({ unique: true })
  public readonly slug: string;
}
