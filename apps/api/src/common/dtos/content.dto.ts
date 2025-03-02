import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsOptional, IsString, MaxLength } from 'class-validator';
import { CrudValidationGroups } from '@dataui/crud';
import { IsSlug } from '@/legacy/common/decorators/isslug.decorator';
import { LocalizableResourceDto } from '@/common/dtos/localizable-resource.dto';

export abstract class ContentDto extends LocalizableResourceDto {
  @ApiProperty({ required: true })
  @IsSlug()
  @IsString()
  @MaxLength(255)
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  public readonly slug: string;
}
