import { ApiProperty } from '@nestjs/swagger';

export class OkDto {
  @ApiProperty({ type: Boolean })
  success?: boolean;
  @ApiProperty({ type: String })
  message?: string;
}
