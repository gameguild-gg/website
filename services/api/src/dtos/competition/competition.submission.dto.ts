import { ApiProperty } from '@nestjs/swagger';
// This is a hack to make Multer available in the Express namespace
import 'multer'; // a hack to make Multer available in the Express namespace

export class CompetitionSubmissionDto {
  @ApiProperty({ type: 'string', format: 'binary', required: true, description: 'The zip file containing the chess bot code' })
  file: Express.Multer.File;
}
