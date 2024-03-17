import { Column, Entity, Index, ManyToOne, OneToMany } from "typeorm";
import { ContentBase } from "./content.base";
import { LectureEntity } from "./lecture.entity";
import { ChapterEntity } from "./chapter.entity";
import { UserEntity } from "../../user/entities";

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
  
  // social tags. todo: create a tag entity
  
  // author
  @ManyToOne(() => UserEntity, (user) => user.courses)
  author: UserEntity;
  
  // a course have many lectures
  @OneToMany(() => LectureEntity, (lecture) => lecture.course)
  lectures: LectureEntity[];

  // a course have many chapters
  @OneToMany(() => ChapterEntity, (chapter) => chapter.course)
  chapters: ChapterEntity[];
}
