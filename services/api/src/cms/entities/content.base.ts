import { EntityBase } from '../../common/entities/entity.base';
import { Column, Index } from 'typeorm';
import { VisibilityEnum } from './visibility.enum';
import { ContentBaseDto } from '../dtos/content.base.dto';
import { ApiProperty } from '@nestjs/swagger';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import { IsNotEmpty, IsString, MaxLength } from 'class-validator';

// todo: move some of these fields to a more basic entity and add abstract classes to specific intents
export abstract class ContentBase extends EntityBase implements ContentBaseDto {
  @Column({ length: 255, nullable: false })
  @Index({ unique: true })
  @ApiProperty()
  @IsSlug()
  @MaxLength(255, { message: 'slug is too long, max 255' })
  @IsNotEmpty({ message: 'slug is required' })
  @IsString({ message: 'slug must be a string' })
  slug: string;

  @Column({ length: 1024, nullable: false })
  @Index({ unique: false })
  @ApiProperty()
  @MaxLength(255, { message: 'title is too long, max 255' })
  @IsNotEmpty({ message: 'title is required' })
  @IsString({ message: 'title must be a string' })
  title: string;

  @Column({ length: 1024, nullable: false })
  @ApiProperty()
  @MaxLength(1024, { message: 'summary is too long, max 1024' })
  @IsNotEmpty({ message: 'summary is required' })
  @IsString({ message: 'summary must be a string' })
  summary: string;

  @Column({ type: 'text', nullable: false })
  @IsString({ message: 'body must be a string' })
  @MaxLength(1024 * 64, { message: 'body is too long, max 64k chars' })
  @IsNotEmpty({ message: 'body is required' })
  // todo: create a better typing to this. avoid using object
  body: string;

  @Index({ unique: false })
  @Column({
    type: 'enum',
    enum: VisibilityEnum,
    default: VisibilityEnum.DRAFT,
    nullable: false,
  })
  visibility: VisibilityEnum;

  // asset image
  @Column({ nullable: true, default: '' })
  thumbnail: string;

  // todo: create availability dates
  // @Column({
  //   type: 'enum',
  //   enum: ContentTypeEnum,
  //   default: ContentTypeEnum.NONE,
  // })
  // type: ContentTypeEnum;

  // todo: how to make this a generic link to any entity inherited from this class?
  // @OneToMany((type) => TagEntity, (tag) => tag.content)
  // tags: TagEntity[];
}
