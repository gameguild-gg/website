import { ContentBase } from "./content.base";
import { PostTypeEnum } from "./post-type.enum";
import { Column, Entity, ManyToMany } from "typeorm";
import { UserEntity } from "../../user/entities";

@Entity({name: "post"})
export class PostEntity extends ContentBase {
  @Column({type: 'enum', enum: PostTypeEnum, default: PostTypeEnum.BLOG, nullable: false})
  PostType: PostTypeEnum;
  
  // todo: tags
  
  // A post can be owned by many users
  @ManyToMany(() => UserEntity, (user) => user.posts)
  owners: UserEntity[];
}