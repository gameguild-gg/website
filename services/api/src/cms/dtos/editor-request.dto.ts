import { IdDto } from '../../auth/dtos/id.dto';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ValidateNested } from 'class-validator';

export class EditorRequestDto extends IdDto {
  @ApiProperty({ type: () => IdDto })
  @Type(() => IdDto)
  @ValidateNested()
  editor: IdDto;
}
