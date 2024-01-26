import {
  Body,
  Controller,
  Get,
  PayloadTooLargeException,
  Post,
  UnauthorizedException, UnsupportedMediaTypeException,
  UploadedFile,
  UseInterceptors
} from "@nestjs/common";
import { ApiConsumes, ApiTags } from '@nestjs/swagger';
import { CompetitionService } from './competition.service';
import { FileInterceptor } from '@nestjs/platform-express';
import { TerminalDto } from './dtos/terminal.dto';

import { CompetitionSubmissionDto } from './dtos/competition.submission.dto';

@Controller('Competitions')
@ApiTags('competitions')
export class CompetitionController {
  constructor(public service: CompetitionService) {}

  @Post('/submit/:username/:password')
  @ApiConsumes('multipart/form-data')
  @UseInterceptors(FileInterceptor('file'))
  async submit(
    @Body() data: CompetitionSubmissionDto,
    @UploadedFile() file: Express.Multer.File,
  ): Promise<TerminalDto[]> {
    if (file.size > 1024 * 50)
      throw new PayloadTooLargeException('File too large. It should be < 10kb');

    const user =
      await this.service.authService.loginGetUserFromUsernamePassword({
        username: data.username,
        password: data.password,
      });
    if (!user) throw new UnauthorizedException('Invalid credentials');

    // // From here to below, it is not working.
    // if (file.mimetype !== 'application/zip')
    //   throw new ('Invalid file type');
    if (file.originalname.split('.').pop() !== 'zip')
      throw new UnsupportedMediaTypeException(
        'Invalid file type. Submit zip file.',
      );

    // store the submission in the database.
    await this.service.storeSubmission({ user: user, file: file.buffer });

    // return error or success
    return this.service.prepareLastUserSubmission(user);
  }

  @Get('/run')
  async run() {
    return this.service.run();
  }

  @Get('/RandomMap')
  async RandomMap(): Promise<string> {
    return this.service.generateInitialMap();
  }
}
