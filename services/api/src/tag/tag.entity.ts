import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { EntityBase } from '../common/entities/entity.base';
import { ContentBase } from '../cms/entities/content.base';

@Entity({ name: 'tag' })
export class TagEntity extends EntityBase {
  @Column({ length: 50, nullable: false, default: '' })
  @Index({ unique: true })
  tag: string;

  // @ManyToOne((type) => ContentBase, (content) => content.tags)
  // content: ContentBase;
}
