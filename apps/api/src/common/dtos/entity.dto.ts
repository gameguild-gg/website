import { CrudValidationGroups } from '@dataui/crud';
import { IsEmpty, IsOptional, IsUUID } from 'class-validator';
import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';

export abstract class EntityDto {
  @ApiProperty({ type: 'string', format: 'uuid' })
  @ApiPropertyOptional()
  @IsUUID('4')
  @IsOptional()
  @IsEmpty({ groups: [CrudValidationGroups.CREATE] })
  public readonly id: string;

  @ApiProperty({ type: 'string', format: 'date-time' })
  @IsOptional()
  @IsEmpty({ groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE] })
  public readonly createdAt: Date;

  @ApiProperty({ type: 'string', format: 'date-time' })
  @IsOptional()
  @IsEmpty({ groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE] })
  public readonly updatedAt: Date;
}
