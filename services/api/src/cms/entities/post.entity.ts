import { Column, Entity, ManyToMany } from 'typeorm';
import { ContentBase } from './content.base';
import { PostTypeEnum } from './post-type.enum';
import { UserEntity } from '../../user/entities';

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

  // A post can be owned by many users
  @ManyToMany(() => UserEntity, (user) => user.posts)
  owners: UserEntity[];
}
