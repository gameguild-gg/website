import { EntityDto } from '@/common/dtos/entity.dto';
import { ApiProperty } from '@nestjs/swagger';
import { IsSlug } from '@/legacy/common/decorators/isslug.decorator';
import { IsNotEmpty, IsOptional, IsString, MaxLength } from 'class-validator';
import { CrudValidationGroups } from '@dataui/crud';

export abstract class ContentDto extends EntityDto {
  @ApiProperty({ required: true })
  @IsSlug()
  @IsString()
  @MaxLength(255)
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  public readonly slug: string;
}
