import { ContentBase } from './content.base';
import { Column, Entity, Index, ManyToOne, OneToMany } from 'typeorm';
import { CourseEntity } from './course.entity';
import { LectureEntity } from './lecture.entity';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  ValidateNested,
} from 'class-validator';

@Entity({ name: 'chapter' })
export class ChapterEntity extends ContentBase {
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty({ required: false, default: 0 })
  @IsOptional()
  @IsNumber({}, { message: 'error.IsNumber: order should be a number' })
  order: number;

  // a chapter belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.chapters)
  @ApiProperty({ type: () => CourseEntity, required: true })
  @ValidateNested()
  @Type(() => CourseEntity)
  course: CourseEntity;

  @OneToMany(() => LectureEntity, (lecture) => lecture.chapter)
  @ApiProperty({ type: () => LectureEntity, isArray: true, required: false })
  @IsOptional()
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => LectureEntity)
  lectures: LectureEntity[];
}
