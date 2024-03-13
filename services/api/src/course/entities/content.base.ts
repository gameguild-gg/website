import { EntityBase } from "../../common/entities/entity.base";
import { Column, Index, OneToMany } from "typeorm";
import { TagEntity } from "../../tag/tag.entity";
import { VisibilityEnum } from "./visibility.enum";

export abstract class ContentBase extends EntityBase {
  // todo: improve this type 
  @Column({ type: 'jsonb', default: {}, nullable: true})
  lexical: object;
  
  @Column({length: 1024, nullable: true, default: ''})
  title: string;
  
  @Column({length: 255, nullable: true, default: ''})
  slug: string;
  
  @Column({length: 1024, nullable: true, default: ''})
  summary: string;
  
  @Index({unique: false})
  @Column({type: 'enum', enum: VisibilityEnum, default: VisibilityEnum.DRAFT})
  visibility: VisibilityEnum;
  
  @OneToMany((type) => TagEntity, (tag) => tag.content)
  tags: TagEntity[];
}
