import { ApiProperty } from '@nestjs/swagger';
import { PrimaryGeneratedColumn } from 'typeorm';
import { IsEmpty, IsNotEmpty, IsOptional, IsUUID } from 'class-validator';
import { CrudValidationGroups } from '@dataui/crud';

export class IdDto {
  @ApiProperty({ type: 'string', format: 'uuid' })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional({ groups: [CrudValidationGroups.CREATE] })
  @IsNotEmpty({
    message: 'error.isNotEmpty: id must not be empty',
    groups: [CrudValidationGroups.UPDATE],
  })
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE],
    message: 'error.isEmpty: id must be empty on create operations',
  })
  @IsUUID('4', {
    message: 'error.isUUID: id is not a valid UUID',
    groups: [CrudValidationGroups.UPDATE],
  })
  readonly id: string;
}
