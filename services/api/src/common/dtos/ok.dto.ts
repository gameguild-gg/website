import { ApiProperty } from '@nestjs/swagger';

export class OkDto {
  @ApiProperty()
  success: boolean;
  @ApiProperty()
  message: string;
}
