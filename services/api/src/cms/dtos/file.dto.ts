import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty } from 'class-validator';

export class FileDto {
  @ApiProperty()
  @IsNotEmpty()
  name: string;

  @ApiProperty({
    oneOf: [{ type: 'string' }, { type: 'Uint8Array' }],
    description: 'type one of Uint8Array | string',
  })
  @IsNotEmpty()
  content: Uint8Array | string;
}
