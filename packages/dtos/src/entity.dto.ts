import { ApiProperty } from '@nestjs/swagger';

export class EntityDto {
  @ApiProperty()
  id: string;
  @ApiProperty()
  createdAt: Date;
  @ApiProperty()
  updatedAt: Date;
}
