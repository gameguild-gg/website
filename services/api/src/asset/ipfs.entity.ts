import { Column, Entity, Index, ManyToOne } from "typeorm";
import { EntityBase } from "../common/entities/entity.base";

@Entity({ name: 'asset' })
export class IpfsEntity extends EntityBase {
  @Column({nullable: false})
  @Index({unique: false})
  CID: string;
  
  // todo: make mime type an enum
  @Index({unique: false})
  @Column({nullable: false})
  mimeType: string;
  
  @Index({unique: false})
  @Column({nullable: false})
  fileName: string;
  
  // todo: deal with reference counting. how many other entities are using this asset?
}