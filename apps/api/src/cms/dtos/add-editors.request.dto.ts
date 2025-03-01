import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsUUID, MinLength } from 'class-validator';

export class AddEditorsRequestDto {
  @ApiProperty({ isArray: true, type: 'string' })
  @IsArray()
  @MinLength(1, { message: 'error.Length: Editors must be at least 1' })
  @IsUUID('4', { each: true })
  editors: string[];
}
