import { IsString, IsNotEmpty, IsEnum } from 'class-validator';
import { VisibilityEnum } from '../entities/visibility.enum';

export class CreateProjectDto {
  @IsString()
  @IsNotEmpty()
  title: string;

  @IsString()
  summary: string;

  @IsString()
  body: string;

  @IsString()
  @IsNotEmpty()
  slug: string;

  @IsEnum(VisibilityEnum)
  visibility: VisibilityEnum;

  @IsString()
  thumbnail: string;
}
