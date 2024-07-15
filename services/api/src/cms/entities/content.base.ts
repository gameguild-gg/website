import { EntityBase } from '../../common/entities/entity.base';
import { Column, Index, OneToMany } from 'typeorm';
import { TagEntity } from '../../tag/tag.entity';
import { VisibilityEnum } from './visibility.enum';
import { ContentTypeEnum } from './content-type.enum';

export abstract class ContentBase extends EntityBase {
  @Column({ length: 255, nullable: true, default: '' })
  // todo: create dto and create a IsSlug decorator
  slug: string;

  @Column({ length: 1024, nullable: true, default: '' })
  title: string;

  @Column({ length: 1024, nullable: true, default: '' })
  summary: string;

  @Column({ type: 'jsonb', default: {}, nullable: true })
  // todo: create a better typing to this. avoid using object
  body: object;

  @Index({ unique: false })
  @Column({ type: 'enum', enum: VisibilityEnum, default: VisibilityEnum.DRAFT })
  visibility: VisibilityEnum;

  // asset image
  @Column({ nullable: true, default: '' })
  thumbnail: string;

  // todo: create availability dates
  // @Column({
  //   type: 'enum',
  //   enum: ContentTypeEnum,
  //   default: ContentTypeEnum.NONE,
  // })
  // type: ContentTypeEnum;

  // todo: how to make this a generic link to any entity inherited from this class?
  // @OneToMany((type) => TagEntity, (tag) => tag.content)
  // tags: TagEntity[];
}
