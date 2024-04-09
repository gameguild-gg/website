import { Column, Entity, Index, ManyToOne, OneToMany } from 'typeorm';
import { ContentBase } from './content.base';
import { LectureEntity } from './lecture.entity';
import { ChapterEntity } from './chapter.entity';
import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';

@Entity({ name: 'course' })
export class CourseEntity extends ContentBase {
  // todo: ensure 2 digits after decimal only
  @Column({ nullable: true, default: 0, type: 'float' })
  @Index({ unique: false })
  @ApiProperty()
  price: number;

  // subscriptions access
  @Column({ nullable: false, default: true })
  @Index({ unique: false })
  @ApiProperty()
  subscriptionAccess: boolean;

  // social tags. todo: create a tag entity

  // author
  @ManyToOne(() => UserEntity, (user) => user.courses)
  @ApiProperty({ type: () => UserEntity })
  author: UserEntity;

  // appliers
  @ManyToOne(() => UserEntity, (user) => user.courses)
  @ApiProperty({ type: () => UserEntity })
  applicants: UserEntity;

  // a course have many lectures
  @OneToMany(() => LectureEntity, (lecture) => lecture.course)
  @ApiProperty({ type: () => LectureEntity, isArray: true })
  lectures: LectureEntity[];

  // a course have many chapters
  @OneToMany(() => ChapterEntity, (chapter) => chapter.course)
  @ApiProperty({ type: () => ChapterEntity, isArray: true })
  chapters: ChapterEntity[];
}
