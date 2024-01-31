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
import {CompetitionGame} from "./entities/competition.submission.entity";
import {Public} from "../auth";
import {ChessMoveRequestDto} from "./dtos/chess-move-request.dto";
import {ChessMatchRequestDto} from "./dtos/chess-match-request.dto";

@Controller('Competitions')
@ApiTags('competitions')
export class CompetitionController {
  constructor(public service: CompetitionService) {}

  // @Post('/CTC/submit')
  // @ApiConsumes('multipart/form-data')
  // @UseInterceptors(FileInterceptor('file'))
  // async submit(
  //   @Body() data: CompetitionSubmissionDto,
  //   @UploadedFile() file: Express.Multer.File,
  // ): Promise<TerminalDto[]> {
  //   if (file.size > 1024 * 50)
  //     throw new PayloadTooLargeException('File too large. It should be < 10kb');
  //
  //   const user =
  //     await this.service.authService.validateLocalSignIn({
  //       email: data.username,
  //       password: data.password,
  //     });
  //   if (!user) throw new UnauthorizedException('Invalid credentials');
  //
  //   // // From here to below, it is not working.
  //   // if (file.mimetype !== 'application/zip')
  //   //   throw new ('Invalid file type');
  //   if (file.originalname.split('.').pop() !== 'zip')
  //     throw new UnsupportedMediaTypeException(
  //       'Invalid file type. Submit zip file.',
  //     );
  //
  //   // store the submission in the database.
  //   await this.service.storeSubmission({ user: user, file: file.buffer, gameType: CompetitionGame.CatchTheCat });
  //
  //   // return error or success
  //   return this.service.prepareLastUserSubmission(user);
  // }
  //
  // @Get('/CTC/run')
  // async run() {
  //   return this.service.run();
  // }
  //
  // @Get('/CTC/RandomMap')
  // async RandomMap(): Promise<string> {
  //   return this.service.generateInitialMap();
  // }

  // todo: Add login protections here and remove the password requirement.
  @Post('/Chess/submit')
  @ApiConsumes('multipart/form-data')
  @UseInterceptors(FileInterceptor('file'))
  @Public()
  async submitChessAgent(
    @Body() data: CompetitionSubmissionDto,
    @UploadedFile() file: Express.Multer.File,
  ): Promise<TerminalDto[]> {
    if (file.size > 1024 * 1024 * 10)
      throw new PayloadTooLargeException('File too large. It should be < 10mb');

    const user =
      await this.service.authService.validateLocalSignIn({
        email: data.username,
        password: data.password,
        username: data.username,
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
    await this.service.storeSubmission({ user: user, file: file.buffer, gameType: CompetitionGame.Chess });

    // return error or success
    return this.service.prepareLastChessSubmission(user);
  }
  
  @Get('/Chess/ListAgents')
  @Public()
  async ListChessAgents(): Promise<string[]> {
    return this.service.listChessAgents();
  }
  
  @Post('/Chess/Move')
  @Public()
  async RequestChessMove(@Body() data: ChessMoveRequestDto): Promise<string> {
    return this.service.RequestChessMove(data);
  }
  
  @Post('/Chess/RunMatch')
  @Public()
  async RunChessMatch(@Body() data: ChessMatchRequestDto): Promise<string> {
    return this.service.RunChessMatch(ChessMatchRequestDto);
  }
}
