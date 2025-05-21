import { CrudValidationGroups } from '@dataui/crud';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsOptional, IsString, MaxLength } from 'class-validator';

import { LocalizableResourceDto } from '@/common/dtos/localizable-resource.dto';

// import { IsSlug } from '@/legacy/common/decorators/isslug.decorator';

export abstract class ContentDto extends LocalizableResourceDto {
  @ApiProperty({ required: true })
  // @IsSlug()
  @IsString()
  @MaxLength(255)
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  public readonly slug: string;
}
