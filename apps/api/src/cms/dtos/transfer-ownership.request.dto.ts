import { ApiProperty } from '@nestjs/swagger';
import { ValidateNested } from 'class-validator';
import { IdDto } from '../../auth/dtos/id.dto';
import { Type } from 'class-transformer';

export class TransferOwnershipRequestDto extends IdDto {
  @ApiProperty({ type: () => IdDto })
  @Type(() => IdDto)
  @ValidateNested()
  newUser: IdDto;
}
