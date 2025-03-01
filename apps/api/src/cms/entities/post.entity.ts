import { Column, Entity } from 'typeorm';
import { ContentBase } from './content.base';
import { PostTypeEnum } from './post-type.enum';

// todo: move to use permissionEntity instead
@Entity({ name: 'post' })
export class PostEntity extends ContentBase {
  @Column({
    type: 'enum',
    enum: PostTypeEnum,
    default: PostTypeEnum.BLOG,
    nullable: false,
  })
  PostType: PostTypeEnum;
}
