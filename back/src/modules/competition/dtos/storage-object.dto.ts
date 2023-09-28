import { ApiProperty } from '@nestjs/swagger';
import { IsString } from 'class-validator';

export class StorageObjectDto {
  @ApiProperty({ required: true })
  @IsString()
  email: string;

  @ApiProperty({ type: 'string', format: 'number', required: false })
  @IsString()
  password: string;

  @ApiProperty({ type: 'string', format: 'binary', required: true })
  file: Express.Multer.File;
}
