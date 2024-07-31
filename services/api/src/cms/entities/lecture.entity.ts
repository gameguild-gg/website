import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { ContentBase } from './content.base';
import { CourseEntity } from './course.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, IsNumber, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity({ name: 'lecture' })
export class LectureEntity extends ContentBase {
  // todo: mimic linkedin behavior where you can access some lectures for free
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: order should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: order should be a number' })
  order: number;

  // a lecture belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.lectures)
  @ApiProperty({ type: () => CourseEntity })
  @ValidateNested()
  @Type(() => CourseEntity)
  course: CourseEntity;

  // a lecture belongs to a chapter
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  @ApiProperty({ type: () => CourseEntity })
  @ValidateNested()
  @Type(() => CourseEntity)
  chapter: CourseEntity;
}
