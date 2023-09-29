import {
  Body,
  Controller,
  PayloadTooLargeException,
  Post,
  UnauthorizedException,
  UploadedFile,
  UseInterceptors,
} from '@nestjs/common';
import { ApiConsumes, ApiTags } from '@nestjs/swagger';
import { CompetitionService } from './competition.service';
import fs from 'fs/promises';
import process from 'process';
import * as fse from 'fs-extra';
import { FileInterceptor } from '@nestjs/platform-express';
import { TerminalDto } from './dtos/terminal.dto';
import extract from 'extract-zip';
import { CompetitionSubmissionDto } from './dtos/competition.submission.dto';
const util = require('util');

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
    if (file.size > 1024 * 10)
      throw new PayloadTooLargeException('File too large. It should be < 10kb');

    const user =
      await this.service.authService.loginGetUserFromUsernamePassword({
        username: data.username,
        password: data.password,
      });
    if (!user) throw new UnauthorizedException('Invalid credentials');

    // From here to below, it is not working.
    if (file.mimetype !== 'application/zip')
      throw new Error('Invalid file type');

    // store the submission in the database.
    await this.service.storeSubmission({ user: user, file: file.buffer });

    data.file = file;

    // rename the file as datetime.zip
    // store the zip file in the user's folder username/zips
    await fs.mkdir('submissions/' + data.username + '/zips', {
      recursive: true,
    });
    const zipPath =
      'submissions/' +
      data.username +
      '/zips/' +
      this.service.getDate() +
      '.zip';
    await fs.writeFile(zipPath, data.file.buffer);
    const sourceFolder =
      process.cwd() + '/submissions/' + data.username + '/src';

    // clear the user's folder username/src
    await fs.rm(sourceFolder, { recursive: true, force: true });
    await fs.mkdir(sourceFolder, { recursive: true });

    // unzip the file into the user's folder username/src
    await extract(zipPath, { dir: sourceFolder });

    // copy the tests folder, main.cpp, cmakefile, functions.h, functions.cpp to the user's folder username/src
    await fse.copy('assets/catchthecat/tests', sourceFolder + '/tests', {
      overwrite: true,
    });
    await fse.copy('assets/catchthecat/main.cpp', sourceFolder + '/main.cpp', {
      overwrite: true,
    });
    await fse.copy(
      'assets/catchthecat/CMakeLists.txt',
      sourceFolder + '/CMakeLists.txt',
      {
        overwrite: true,
      },
    );
    await fse.copy(
      'assets/catchthecat/functions.h',
      sourceFolder + '/functions.h',
      {
        overwrite: true,
      },
    );
    await fse.copy('assets/catchthecat/IAgent.h', sourceFolder + '/IAgent.h', {
      overwrite: true,
    });
    const outs: TerminalDto[] = [];
    // compile the users code
    outs.push(
      await this.service.runCommand(
        'cmake -S ' + sourceFolder + ' -B ' + sourceFolder + '/build',
      ),
    );
    outs.push(
      await this.service.runCommand('cmake --build ' + sourceFolder + '/build'),
    );
    // run automated tests
    outs.push(
      await this.service.runCommand(
        'cmake --build ' +
          sourceFolder +
          '/build --target StudentSimulation-test',
      ),
    );
    // store the compiled code in the user's folder username/
    if (fse.existsSync(sourceFolder + '/build/StudentSimulation'))
      await fse.copy(
        sourceFolder + '/build/StudentSimulation',
        sourceFolder + '/../StudentSimulation',
        { overwrite: true },
      );
    // return error or success
    return outs;
  }
}
