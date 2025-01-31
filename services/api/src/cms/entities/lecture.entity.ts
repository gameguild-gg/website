import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { ContentBase } from './content.base';
import { CourseEntity } from './course.entity';
import { ApiProperty } from '@nestjs/swagger';
import {
  IsArray,
  IsEnum,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  ValidateNested,
} from 'class-validator';
import { Type } from 'class-transformer';
import { ChapterEntity } from './chapter.entity';

export enum LectureType {
  MARKDOWN = 'markdown',
  YOUTUBE = 'youtube',
  LEXICAL = 'lexical',
  REVEAL = 'reveal',
  HTML = 'html',
  PDF = 'pdf',
  IMAGE = 'image',
  VIDEO = 'video',
  AUDIO = 'audio',
  CODE = 'code',
  LINK = 'link',
}

@Entity({ name: 'lecture' })
export class LectureEntity extends ContentBase {
  // todo: mimic linkedin behavior where you can access some lectures for free
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: order should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: order should be a number' })
  order: number;

  @ApiProperty({ enum: LectureType, default: LectureType.MARKDOWN })
  @Column({
    nullable: false,
    type: 'enum',
    enum: LectureType,
    default: LectureType.MARKDOWN,
  })
  @Index({ unique: false })
  @IsOptional()
  @IsEnum(LectureType)
  renderer: LectureType;

  // a lecture belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.lectures)
  @ApiProperty({ type: () => CourseEntity })
  @ValidateNested()
  @Type(() => CourseEntity)
  course: CourseEntity;

  // a lecture belongs to a chapter
  @ManyToOne(() => ChapterEntity, (chapter) => chapter.lectures)
  @ApiProperty({ type: () => ChapterEntity })
  @ValidateNested()
  @Type(() => ChapterEntity)
  chapter: ChapterEntity;
}
