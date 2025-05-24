import { Column, Entity, Index, ManyToOne, OneToMany } from 'typeorm';
import { ContentBase } from './content.base';
import { LectureEntity } from './lecture.entity';
import { ChapterEntity } from './chapter.entity';
import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsBoolean, IsNotEmpty, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity({ name: 'course' })
export class CourseEntity extends ContentBase {
  // todo: ensure 2 digits after decimal only
  @Column({
    nullable: true,
    default: 0,
    type: 'decimal',
    precision: 7,
    scale: 2,
  })
  @Index({ unique: false })
  @ApiProperty()
  price: number;

  // subscriptions access
  @Column({ nullable: false, default: true })
  @Index({ unique: false })
  @IsNotEmpty({
    message: 'error.IsNotEmpty: subscriptionAccess should not be empty',
  })
  @IsBoolean({
    message: 'error.IsBoolean: subscriptionAccess should be boolean',
  })
  @ApiProperty()
  subscriptionAccess: boolean;

  // social tags. todo: create a tag entity

  // a course have many lectures
  @OneToMany(() => LectureEntity, (lecture) => lecture.course)
  @ApiProperty({ type: LectureEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: lectures should be an array' })
  @ValidateNested({ each: true })
  @Type(() => LectureEntity)
  lectures: LectureEntity[];

  // a course have many chapters
  @OneToMany(() => ChapterEntity, (chapter) => chapter.course)
  @ApiProperty({ type: ChapterEntity, isArray: true })
  @IsArray({ message: 'error.IsArray: chapters should be an array' })
  @ValidateNested({ each: true })
  @Type(() => ChapterEntity)
  chapters: ChapterEntity[];

  // todo: add quizzes, assignments, projects and all other types of content for course

  // todo: denormalize the number of lectures, chapters, and students
  // enrollments
  // rating, feedback, and reviews
  // hours of content, including video and estimated time of words per lecture
  // estimated time for homeworks and projects
  // number of entries / lessons / lectures
}
