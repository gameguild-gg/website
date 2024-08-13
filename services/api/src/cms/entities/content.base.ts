import { Column, Index } from 'typeorm';
import { VisibilityEnum } from './visibility.enum';
import { ApiProperty } from '@nestjs/swagger';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import { IsNotEmpty, IsString, MaxLength } from 'class-validator';
import { WithRolesEntity } from '../../auth/entities/with-roles.entity';

// todo: move some of these fields to a more basic entity and add abstract classes to specific intents
export abstract class ContentBase extends WithRolesEntity {
  @Column({ length: 255, nullable: false, type: 'varchar' })
  @Index({ unique: true })
  @ApiProperty()
  @IsSlug({ message: 'error.slug: slug field must be a valid slug' })
  @MaxLength(255, {
    message: 'error.maxLength: slug field is too long: max 255',
  })
  @IsNotEmpty({ message: 'error.notEmpty: slug is required' })
  @IsString({ message: 'error.isString: slug must be a string' })
  slug: string;

  @Column({ length: 1024, nullable: false, type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty()
  @MaxLength(255, { message: 'error.maxLength: title is too long, max 255' })
  @IsNotEmpty({ message: 'error.isNotEmpty: title is required' })
  @IsString({ message: 'error.isString: title must be a string' })
  title: string;

  @Column({ length: 1024, nullable: false })
  @ApiProperty()
  @MaxLength(1024, {
    message: 'error.maxLength: summary is too long, max 1024',
  })
  @IsNotEmpty({ message: 'error.isNotEmpty: summary is required' })
  @IsString({ message: 'error.isString: summary must be a string' })
  summary: string;

  @Column({ type: 'text', nullable: false })
  @IsString({ message: 'error.isString: body must be a string' })
  @MaxLength(1024 * 64, {
    message: 'error.maxLength: body is too long, max 64kb',
  })
  @IsNotEmpty({ message: 'error.isNotEmpty: body is required' })
  @ApiProperty()
  // todo: guarantee the type here, md or anything else
  body: string;

  @Index({ unique: false })
  @Column({
    type: 'enum',
    enum: VisibilityEnum,
    default: VisibilityEnum.DRAFT,
    nullable: false,
  })
  @ApiProperty({ enum: VisibilityEnum })
  visibility: VisibilityEnum;

  // asset image
  @Column({ nullable: true, default: '', type: 'varchar', length: 1024 })
  @ApiProperty()
  thumbnail: string;

  constructor(partial?: Partial<ContentBase>) {
    super(partial);
    Object.assign(this, partial);
  }
}
