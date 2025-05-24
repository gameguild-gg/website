import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsOptional, IsString, IsUrl, Length } from 'class-validator';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import { VisibilityEnum } from '../entities/visibility.enum';
import { ContentBase } from '../entities/content.base';
import { ProjectEntity } from '../entities/project.entity';

export class CreateProjectDto
  implements Omit<ProjectEntity, 'createdAt' | 'editors' | 'owner' | 'id' | 'updatedAt' | 'thumbnail' | 'banner' | 'screenshots' | 'versions' | 'tickets'>
{
  @ApiProperty()
  @IsNotEmpty()
  @IsString()
  @Length(1, 255)
  @IsSlug()
  slug: string;

  @ApiProperty()
  @IsNotEmpty()
  @IsString()
  @Length(1, 255)
  title: string;

  @ApiProperty()
  @IsOptional()
  @IsString()
  @Length(1, 1024)
  summary: string;

  @ApiProperty()
  @IsOptional()
  @IsString()
  @Length(1, 1024 * 64)
  body: string;

  @ApiProperty({ enum: VisibilityEnum })
  @IsOptional()
  @IsEnum(VisibilityEnum)
  visibility: VisibilityEnum;
}
