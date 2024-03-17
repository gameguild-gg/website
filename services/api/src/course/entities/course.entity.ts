import { Column, Entity, Index } from "typeorm";
import { ContentBase } from "./content.base";

@Entity({ name: 'course' })
export class CourseEntity extends ContentBase {
  // todo: ensure 2 digits after decimal only
  @Column({ nullable: true, default: 0, type: 'float' })
  @Index({unique: false})
  price: number;
  
  // subscriptions access
  @Column({nullable: false, default: true})
  @Index({unique: false})
  subscriptionAccess: boolean;
  
  // asset image
  // social tags
  // author
  // chapters
}
