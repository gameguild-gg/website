import { Column, Entity, Index, ManyToOne } from "typeorm";
import { ContentBase } from "./content.base";
import { CourseEntity } from "./course.entity";

@Entity({name: "lecture"})
export class LectureEntity extends ContentBase {
  // todo: mimic linkedin behavior where you can access some lectures for free
  
  @Column({nullable: false, type: 'float', default: 0})
  @Index({unique: false})
  order: number;
  
  // a lecture belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.lectures)
  course: CourseEntity;
  
  // a lecture belongs to a chapter
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  chapter: CourseEntity;
}