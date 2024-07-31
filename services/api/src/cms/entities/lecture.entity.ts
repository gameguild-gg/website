import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { ContentBase } from './content.base';
import { CourseEntity } from './course.entity';
import { ApiProperty } from '@nestjs/swagger';

@Entity({ name: 'lecture' })
export class LectureEntity extends ContentBase {
  // todo: mimic linkedin behavior where you can access some lectures for free
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty()
  order: number;

  // a lecture belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.lectures)
  @ApiProperty({ type: () => CourseEntity })
  course: CourseEntity;

  // a lecture belongs to a chapter
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  @ApiProperty({ type: () => CourseEntity })
  chapter: CourseEntity;
}
