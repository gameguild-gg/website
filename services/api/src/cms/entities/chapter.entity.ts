import { ContentBase } from "./content.base";
import { Column, Entity, Index, ManyToOne, OneToMany } from "typeorm";
import { CourseEntity } from "./course.entity";
import { LectureEntity } from "./lecture.entity";

@Entity({ name: 'chapter' })
export class ChapterEntity extends ContentBase {
  @Column({nullable: false, type: 'float', default: 0})
  @Index({unique: false})
  order: number;
  
  // a chapter belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  course: CourseEntity;
  
  @OneToMany(() => LectureEntity, (lecture) => lecture.chapter)
  lectures: LectureEntity[]
}