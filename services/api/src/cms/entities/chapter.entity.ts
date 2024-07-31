import { ContentBase } from './content.base';
import { Column, Entity, Index, ManyToOne, OneToMany } from 'typeorm';
import { CourseEntity } from './course.entity';
import { LectureEntity } from './lecture.entity';
import { ApiProperty } from '@nestjs/swagger';

@Entity({ name: 'chapter' })
export class ChapterEntity extends ContentBase {
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty()
  order: number;

  // a chapter belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  @ApiProperty({ type: () => CourseEntity })
  course: CourseEntity;

  @OneToMany(() => LectureEntity, (lecture) => lecture.chapter)
  @ApiProperty({ type: () => LectureEntity, isArray: true })
  lectures: LectureEntity[];

  constructor(partial?: Partial<ChapterEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
