import { Column, Index } from 'typeorm';

import { ContentDto } from '@/common/dtos/content.dto';
import { LocalizableResourceBase } from '@/common/entities/localizable-resource.base';

export abstract class ContentBase extends LocalizableResourceBase implements ContentDto {
  @Column({ type: 'varchar', length: 255, nullable: false })
  @Index({ unique: true })
  public readonly slug: string;
}
