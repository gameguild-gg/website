import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { CrudValidationGroups } from '@dataui/crud';
import { IsEmpty, IsOptional, IsUUID } from 'class-validator';

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

  protected constructor(partial: Partial<typeof this>) {
    Object.assign(this, partial);
  }

  public static create<T extends EntityDto>(this: new (partial: Partial<T>) => T, partial: Partial<T>): T {
    return new this(partial);
  }
}
