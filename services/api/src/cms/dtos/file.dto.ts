import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty } from 'class-validator';

export class FileDto {
  @ApiProperty()
  @IsNotEmpty()
  name: string;

  @ApiProperty({
    oneOf: [
      { type: 'string' },
      { 
        type: 'string', 
        format: 'binary',
        description: 'Binary content represented as base64 string'
      }
    ],
    description: 'File content as string or binary data (base64 encoded)'
  })
  @IsNotEmpty()
  content: Uint8Array | string;
}
