import {
  Body,
  Controller,
  Get,
  Logger,
  Param,
  ParseUUIDPipe,
  PayloadTooLargeException,
  Post,
  UnauthorizedException,
  UnprocessableEntityException,
  UnsupportedMediaTypeException,
  UploadedFile,
  UseInterceptors,
} from '@nestjs/common';
import {
  ApiConsumes,
  ApiCreatedResponse,
  ApiNoContentResponse,
  ApiOkResponse,
  ApiResponse,
  ApiTags,
} from '@nestjs/swagger';
import { CompetitionService } from './competition.service';
import { FileInterceptor } from '@nestjs/platform-express';
import { CompetitionGame } from './entities/competition.submission.entity';
import { CompetitionSubmissionDto } from '../dtos/competition/competition.submission.dto';
import { TerminalDto } from '../dtos/competition/terminal.dto';
import { ChessMoveRequestDto } from '../dtos/competition/chess-move-request.dto';
import { ChessMatchRequestDto } from '../dtos/competition/chess-match-request.dto';
import { ChessMatchResultDto } from '../dtos/competition/chess-match-result.dto';
import { MatchSearchResponseDto } from '../dtos/competition/match-search-response.dto';
import { MatchSearchRequestDto } from '../dtos/competition/match-search-request.dto';
import { CompetitionMatchEntity } from './entities/competition.match.entity';
import { ChessLeaderboardResponseEntryDto } from '../dtos/competition/chess-leaderboard-response.dto';
import { Auth } from '../auth/decorators/http.decorator';
import { AuthUser } from '../auth';
import { UserEntity } from '../user/entities';

import { CompetitionRunSubmissionReportEntity } from './entities/competition.run.submission.report.entity';
import { OkResponse } from '../common/decorators/return-type.decorator';
import { AuthType } from '../auth/guards';
import { AuthenticatedRoute } from '../auth/auth.enum';

@Controller('Competitions')
@ApiTags('competitions')
export class CompetitionController {
  constructor(public service: CompetitionService) {}
  logger: Logger = new Logger(CompetitionController.name);
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
  @ApiResponse({
    type: TerminalDto,
    isArray: true,
  })
  @UseInterceptors(FileInterceptor('file'))
  @Auth(AuthenticatedRoute)
  async submitChessAgent(
    @Body() data: CompetitionSubmissionDto,
    @UploadedFile() file: Express.Multer.File,
    @AuthUser() user: UserEntity,
  ): Promise<TerminalDto[]> {
    if (file.size > 1024 * 1024 * 10)
      throw new PayloadTooLargeException('File too large. It should be < 10mb');

    if (!user) throw new UnauthorizedException('Invalid credentials');

    // // From here to below, it is not working.
    // if (file.mimetype !== 'application/zip')
    //   throw new ('Invalid file type');
    if (file.originalname.split('.').pop() !== 'zip')
      throw new UnsupportedMediaTypeException(
        'Invalid file type. Submit zip file.',
      );

    // store the submission in the database.
    await this.service.storeSubmission({
      user: user,
      file: file.buffer,
      gameType: CompetitionGame.Chess,
    });

    // return error or success
    try {
      return this.service.prepareLastChessSubmission(user);
    } catch (e) {
      this.logger.error(e);
      this.logger.error(JSON.stringify(e));
      throw e;
    }
  }

  @Get('/Chess/ListAgents')
  @ApiResponse({ type: String, isArray: true })
  @Auth(AuthenticatedRoute)
  async ListChessAgents(@AuthUser() user: UserEntity): Promise<string[]> {
    if (!user) throw new UnauthorizedException('Invalid credentials');
    return this.service.listChessAgents();
  }

  @Post('/Chess/Move')
  @ApiResponse({ type: String })
  @Auth(AuthenticatedRoute)
  async RequestChessMove(
    @Body() data: ChessMoveRequestDto,
    @AuthUser() user: UserEntity,
  ): Promise<string> {
    if (!user) throw new UnauthorizedException('Invalid credentials');
    return this.service.RequestChessMove(data);
  }

  @Post('/Chess/RunMatch')
  @ApiResponse({ type: ChessMatchResultDto })
  @Auth(AuthenticatedRoute)
  async RunChessMatch(
    @Body() data: ChessMatchRequestDto,
    @AuthUser() user: UserEntity,
  ): Promise<ChessMatchResultDto> {
    if (!user) throw new UnauthorizedException('Invalid credentials');
    return (await this.service.RunChessMatch(data)).result;
  }

  @Post('/Chess/FindMatches')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: MatchSearchResponseDto,
    isArray: true,
  })
  async FindChessMatchResult(
    @Body() data: MatchSearchRequestDto,
    @AuthUser() user: UserEntity,
  ): Promise<MatchSearchResponseDto[]> {
    if (!user) throw new UnauthorizedException('Invalid credentials');
    // todo: return the result state and reason for the match ending.
    if (data.pageSize > 100)
      throw new UnprocessableEntityException(
        'You can only take 100 matches at a time',
      );

    const ret = await this.service.findMatchesByCriteria({
      where: [
        {
          p1submission: {
            user: {
              username: data.username,
            },
          },
        },
        {
          p2submission: {
            user: {
              username: data.username,
            },
          },
        },
      ],
      order: {
        createdAt: 'DESC',
      },
      take: data.pageSize,
      skip: data.pageId * data.pageSize,
      relations: {
        p1submission: { user: true },
        p2submission: { user: true },
      },
      select: {
        id: true,
        winner: true,
        lastState: true,
        createdAt: true,
        p1submission: { id: true, user: { username: true } },
        p2submission: { id: true, user: { username: true } },
      },
    });

    // convert array of CompetitionMatchEntity to MatchSearchResponseDto
    const converted: MatchSearchResponseDto[] = ret.map(
      (match: CompetitionMatchEntity) => {
        const convertedMatch = {
          id: match.id,
          winner: match.winner,
          lastState: match.lastState,
          players: [
            match.p1submission.user.username,
            match.p2submission.user.username,
          ],
        };
        return convertedMatch as MatchSearchResponseDto;
      },
    );

    return converted;
  }

  @Get('/Chess/Match/:id')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ type: ChessMatchResultDto })
  async GetChessMatchResult(
    @Param('id', ParseUUIDPipe) id: string,
  ): Promise<ChessMatchResultDto> {
    return JSON.parse((await this.service.findMatchById(id)).logs);
  }

  @Get('/Chess/Leaderboard')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: ChessLeaderboardResponseEntryDto,
    isArray: true,
  })
  async GetChessLeaderboard(): Promise<ChessLeaderboardResponseEntryDto[]> {
    return this.service.getLeaderboard();
  }

  @Get('Chess/RunCompetition')
  @Auth(AuthenticatedRoute)
  async RunCompetition() {
    return this.service.runChessCompetition();
  }

  @Get('Chess/LatestCompetitionReport')
  @ApiResponse({
    type: CompetitionRunSubmissionReportEntity,
    isArray: true,
  })
  @Auth(AuthenticatedRoute)
  async GetLatestChessCompetitionReport(
    @AuthUser() user: UserEntity,
  ): Promise<CompetitionRunSubmissionReportEntity[]> {
    if (!user) throw new UnauthorizedException('Invalid credentials');
    return this.service.getLatestChessCompetitionReport();
  }
}
