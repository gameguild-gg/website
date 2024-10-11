import { ApiProperty } from '@nestjs/swagger';
import {
  IsEnum,
  IsNotEmpty,
  IsOptional,
  IsString,
  IsUrl,
  MaxLength,
} from 'class-validator';
import { CrudValidationGroups } from '@dataui/crud';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import { VisibilityEnum } from '../entities/visibility.enum';
import { ProjectEntity } from '../entities/project.entity';

export class CreateProjectDto
  implements
    Omit<
      ProjectEntity,
      | 'createdAt'
      | 'editors'
      | 'owner'
      | 'id'
      | 'updatedAt'
      | 'versions'
      | 'deletedAt'
      | 'tickets'
    >
{
  @ApiProperty()
  @MaxLength(255, { message: 'error.maxLength: title is too long, max 255' })
  @IsNotEmpty({
    message: 'error.isNotEmpty: title is required',
    groups: [CrudValidationGroups.CREATE],
  })
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsString({ message: 'error.isString: title must be a string' })
  title: string;

  @ApiProperty()
  @MaxLength(1024, {
    message: 'error.maxLength: summary is too long, max 1024',
  })
  @IsNotEmpty({ message: 'error.isNotEmpty: summary is required' })
  @IsString({ message: 'error.isString: summary must be a string' })
  summary: string;

  @IsString({ message: 'error.isString: body must be a string' })
  @MaxLength(1024 * 64, {
    message: 'error.maxLength: body is too long, max 64kb',
  })
  @IsNotEmpty({ message: 'error.isNotEmpty: body is required' })
  @ApiProperty()
  // todo: guarantee the type here, md or anything else
  body: string;

  @ApiProperty()
  @IsSlug({ message: 'error.slug: slug field must be a valid slug' })
  @MaxLength(255, {
    message: 'error.maxLength: slug field is too long: max 255',
  })
  @IsNotEmpty({
    message: 'error.notEmpty: slug is required',
    groups: [CrudValidationGroups.CREATE],
  })
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsString({ message: 'error.isString: slug must be a string' })
  slug: string;

  @ApiProperty({ enum: VisibilityEnum })
  @IsOptional()
  @IsEnum(VisibilityEnum, {
    message: 'error.isEnum: visibility must be a valid enum',
  })
  visibility: VisibilityEnum;

  // asset image
  @ApiProperty()
  @IsOptional()
  @IsUrl(
    { require_protocol: true },
    {
      message: 'error.isUrl: thumbnail must be a fully qualified and valid url',
    },
  )
  thumbnail: string;
}

// the generated type does not contain the decorators.
