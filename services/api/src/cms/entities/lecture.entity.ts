import { Column, Entity, Index, ManyToOne } from 'typeorm';
import { ObjectType, Field, registerEnumType } from '@nestjs/graphql';
import { ContentBase } from './content.base';
import { CourseEntity } from './course.entity';
import { ApiProperty, getSchemaPath } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsNumber, IsOptional, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ChapterEntity } from './chapter.entity';
import { CodeAssignmentDto } from '../dtos/code-assignment.dto';

export enum LectureType {
  MARKDOWN = 'markdown',
  YOUTUBE = 'youtube',
  LEXICAL = 'lexical',
  REVEAL = 'reveal',
  HTML = 'html',
  CODE = 'code',
  LINK = 'link',
  // assets types
  PDF = 'pdf',
  IMAGE = 'image',
  VIDEO = 'video',
  AUDIO = 'audio',
}

registerEnumType(LectureType, {
  name: 'LectureType',
});

@Entity({ name: 'lecture' })
@ObjectType()
export class LectureEntity extends ContentBase {
  // todo: mimic linkedin behavior where you can access some lectures for free
  @Column({ nullable: false, type: 'float', default: 0 })
  @Index({ unique: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: order should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: order should be a number' })
  @Field()
  order: number;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ required: false, oneOf: [{ $ref: getSchemaPath(CodeAssignmentDto) }] })
  @IsOptional()
  @Field(() => String, { nullable: true })
  json: CodeAssignmentDto;

  @ApiProperty({
    enum: LectureType,
    default: LectureType.MARKDOWN,
    description: 'Depending of the renderer, the data may be stored in the field body or json, or both.',
  })
  @Column({
    nullable: false,
    type: 'enum',
    enum: LectureType,
    default: LectureType.MARKDOWN,
  })
  @Index({ unique: false })
  @IsOptional()
  @IsEnum(LectureType)
  @Field(() => LectureType)
  renderer: LectureType;

  // a lecture belongs to a course
  @ManyToOne(() => CourseEntity, (course) => course.lectures)
  @ApiProperty({ type: () => CourseEntity })
  @ValidateNested()
  @Type(() => CourseEntity)
  @Field(() => CourseEntity)
  course: CourseEntity;

  // a lecture belongs to a chapter
  @ManyToOne(() => ChapterEntity, (chapter) => chapter.lectures)
  @ApiProperty({ type: () => ChapterEntity })
  @ValidateNested()
  @Type(() => ChapterEntity)
  @Field(() => ChapterEntity)
  chapter: ChapterEntity;
}
