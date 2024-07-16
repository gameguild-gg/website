import { Column, Index } from 'typeorm';
import { VisibilityEnum } from '../entities/visibility.enum';
import { ApiProperty } from '@nestjs/swagger';
import { EntityDto } from '../../dtos/entity.dto';
import { IsNotEmpty, IsString, IsUUID, MaxLength } from 'class-validator';
import { IsSlug } from '../../common/decorators/isslug.decorator';

export class ContentBaseDto extends EntityDto {
  @ApiProperty()
  @IsSlug()
  @IsString({ message: 'slug must be a string' })
  @MaxLength(255, { message: 'slug is too long, max 255' })
  @IsNotEmpty({ message: 'slug is required' })
  slug: string;

  @ApiProperty()
  @MaxLength(1024, { message: 'title is too long, max 1024' })
  @IsNotEmpty({ message: 'title is required' })
  @IsString({ message: 'title must be a string' })
  title: string;

  @ApiProperty()
  @MaxLength(1024, { message: 'summary is too long, max 1024' })
  @IsNotEmpty({ message: 'summary is required' })
  @IsString({ message: 'summary must be a string' })
  summary: string;

  @ApiProperty()
  @IsString({ message: 'body must be a string' })
  @MaxLength(1024 * 64, { message: 'body is too long, max 64k chars' })
  @IsNotEmpty({ message: 'body is required' })
  body: string;

  @ApiProperty({ enum: VisibilityEnum, default: VisibilityEnum.DRAFT })
  visibility: VisibilityEnum;

  @ApiProperty()
  thumbnail: string;
}
