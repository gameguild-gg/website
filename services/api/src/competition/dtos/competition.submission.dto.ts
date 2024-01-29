import { ApiProperty } from '@nestjs/swagger';
import { IsString } from 'class-validator';
// This is a hack to make Multer available in the Express namespace
import 'multer'; // a hack to make Multer available in the Express namespace

export class CompetitionSubmissionDto {
  @ApiProperty({ required: true })
  @IsString()
  username: string;

  @ApiProperty({ type: 'string', required: false })
  @IsString()
  password: string;

  @ApiProperty({ type: 'string', format: 'binary', required: true })
  file: Express.Multer.File;
}
