import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

export class HaveIAppliedToJobResponseDto {
  // Applied
  @ApiProperty({ type: Boolean, isArray: true })
  @IsArray({ message: 'error.IsArray: tags should be an array' })
  @ValidateNested({ each: true })
  @Type(() => Boolean)
  @IsNotEmpty()
  applied: boolean[];
}
