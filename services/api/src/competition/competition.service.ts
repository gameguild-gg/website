import { ConflictException, Injectable, UnprocessableEntityException } from "@nestjs/common";
import { AuthService } from "../auth/auth.service";
import * as util from "util";
import { InjectRepository } from "@nestjs/typeorm";
import { IsNull, Not, Repository } from "typeorm";
import { CompetitionGame, CompetitionSubmissionEntity } from "./entities/competition.submission.entity";
import { CompetitionRunEntity, CompetitionRunState } from "./entities/competition.run.entity";
import { CompetitionMatchEntity, CompetitionWinner } from "./entities/competition.match.entity";
import { promises as fsp } from "fs";
import * as fse from "fs-extra";
import { UserService } from "../user/user.service";
import { LinqRepository } from "typeorm-linq-repository";
import { CompetitionRunSubmissionReportEntity } from "./entities/competition.run.submission.report.entity";
import { UserEntity } from "../user/entities";
import { CleanOptions, ResetMode, simpleGit } from "simple-git";
import * as process from "process";
import * as decompress from "decompress";
import ExecuteCommand, { ExecuteCommandResult } from "../common/execute-command";
import { Chess, Move } from "chess.js";
import { UserProfileService } from "../user/modules/user-profile/user-profile.service";
import { FindManyOptions } from "typeorm/find-options/FindManyOptions";
import { TerminalDto } from "../dtos/competition/terminal.dto";
import { ChessMoveRequestDto } from "../dtos/competition/chess-move-request.dto";
import {
  ChessGameResult,
  ChessGameResultReason,
  ChessMatchResultDto
} from "../dtos/competition/chess-match-result.dto";
import { ChessLeaderboardResponseDto } from "../dtos/competition/chess-leaderboard-response.dto";
import { ChessMatchRequestDto } from "../dtos/competition/chess-match-request.dto";
import { CompetitionRunSubmissionReportDto } from "../dtos/competition/chess-competition-report.dto";
import * as moment from "moment";

const execShPromise = require('exec-sh').promise;

const exec = util.promisify(require('child_process').exec);

class Point2D {
  x: number;
  y: number;
}

enum Turn {
  CAT = 'CAT',
  CATCHER = 'CATCHER',
  CATWIN = 'CATWIN',
  CATCHERWIN = 'CATCHERWIN',
  CATERROR = 'CATERROR',
  CATCHERERROR = 'CATCHERERROR',
}

class LevelMap {
  board: string;
  size: number;
  turn: Turn;
  cat: Point2D;
  time?: number;
  error?: string;

  constructor(data: string) {
    const lines = data.split('\n');
    const firstLine = lines[0].split(' ');
    this.turn = Turn[firstLine[0] as keyof typeof Turn];
    if (this.turn !== Turn.CATCHERERROR && this.turn !== Turn.CATERROR) {
      this.size = parseInt(firstLine[1]);
      this.cat = { x: parseInt(firstLine[2]), y: parseInt(firstLine[3]) };
      this.board = '';
      for (let i = 1; i <= this.size; i++) {
        this.board += lines[i] + '\n';
      }
      if (lines.length > this.size + 1) {
        this.time = parseInt(lines[this.size + 1]);
      }
    } else this.error = lines[0];
  }

  print(): string {
    if (this.time)
      return (
        this.turn +
        ' ' +
        this.size +
        ' ' +
        this.cat.x +
        ' ' +
        this.cat.y +
        '\n' +
        this.board +
        this.time +
        '\n'
      );
    else
      return (
        this.turn +
        ' ' +
        this.size +
        ' ' +
        this.cat.x +
        ' ' +
        this.cat.y +
        '\n' +
        this.board
      );
  }
}

@Injectable()
export class CompetitionService {
  submissionRepositoryLinq: LinqRepository<CompetitionSubmissionEntity>;
  runRepositoryLinq: LinqRepository<CompetitionRunEntity>;
  matchRepositoryLinq: LinqRepository<CompetitionMatchEntity>;
  submissionReportLinq: LinqRepository<CompetitionRunSubmissionReportEntity>;

  constructor(
    public authService: AuthService,
    public userService: UserService,
    public userProfileService: UserProfileService,
    @InjectRepository(CompetitionSubmissionEntity)
    public submissionRepository: Repository<CompetitionSubmissionEntity>,
    @InjectRepository(CompetitionRunEntity)
    public runRepository: Repository<CompetitionRunEntity>,
    @InjectRepository(CompetitionMatchEntity)
    public matchRepository: Repository<CompetitionMatchEntity>,
    @InjectRepository(CompetitionRunSubmissionReportEntity)
    public submissionReportRepository: Repository<CompetitionRunSubmissionReportEntity>,
  ) {
    this.submissionRepositoryLinq = new LinqRepository(
      submissionRepository.manager.connection,
      CompetitionSubmissionEntity,
      {
        primaryKey: (entity) => entity.id,
      },
    );

    this.runRepositoryLinq = new LinqRepository(
      runRepository.manager.connection,
      CompetitionRunEntity,
      {
        primaryKey: (entity) => entity.id,
      },
    );

    this.matchRepositoryLinq = new LinqRepository(
      matchRepository.manager.connection,
      CompetitionMatchEntity,
      {
        primaryKey: (entity) => entity.id,
      },
    );

    // start and delete stale competition runs
    this.deleteStuckCompetition(CompetitionGame.Chess).then();
  }

  getDate(): string {
    return new Date().toISOString();
  }

  async storeSubmission(data: {
    user: UserEntity;
    file: Buffer;
    gameType: CompetitionGame;
  }) {
    const submission = this.submissionRepository.save({
      user: data.user,
      sourceCodeZip: data.file,
      gameType: data.gameType,
    });
    return submission;
  }

  async runCommandSpawn(
    command: string,
  ): Promise<{ stdout: string; stderr: string }> {
    return await execShPromise(command, { maxBuffer: 1024 * 1024 * 50 });
  }

  async runCommand(
    command: string,
    log = true,
    timeout: number = null,
  ): Promise<TerminalDto> {
    let stdout: string, stderr: string;
    try {
      if (timeout === null) {
        const outExec = exec(command);
        outExec.stdout.on('data', (data) => {
          console.log(data);
        });
        const out = await outExec;
        stdout = out.stdout;
        stderr = out.stderr;
      } else {
        const outExec = exec(command, { timeout: timeout });
        outExec.stdout.on('data', (data) => {
          console.log(data);
        });
        const out = await outExec;
        stdout = out.stdout;
        stderr = out.stderr;
      }
      const data: TerminalDto = {
        date: this.getDate(),
        command,
        stdout,
        stderr,
      };
      if (log) await this.appendLog(data);
      return data;
    } catch (err) {
      const data: TerminalDto = {
        date: this.getDate(),
        command: command,
        stdout: null,
        stderr: err,
      };
      if (log) await this.appendLog(data);
      return data;
    }
  }
  async appendLog(data: TerminalDto): Promise<void> {
    await fsp.appendFile('log.txt', JSON.stringify(data) + '\n', 'utf8');
  }

  // async prepareLastUserSubmission(user: UserEntity): Promise<TerminalDto[]> {
  //   const submission = await this.submissionRepository.findOne({
  //     where: { user: { id: user.id } },
  //     order: { createdAt: 'DESC' },
  //   });
  //   const userFolder = process.cwd() + '/submissions/' + user.username;
  //   await fsp.rm(userFolder, { recursive: true, force: true });
  //
  //   // rename the file as datetime.zip
  //   // store the zip file in the user's folder username/zips
  //   await fsp.mkdir('submissions/' + user.username + '/zips', {
  //     recursive: true,
  //   });
  //   const zipPath =
  //     'submissions/' + user.username + '/zips/' + this.getDate() + '.zip';
  //   await fsp.writeFile(zipPath, submission.sourceCodeZip);
  //   const sourceFolder =
  //     process.cwd() + '/submissions/' + user.username + '/src';
  //
  //   // clear the user's folder username/src
  //   await fsp.rm(sourceFolder, { recursive: true, force: true });
  //   await fsp.mkdir(sourceFolder, { recursive: true });
  //
  //   // unzip the file into the user's folder username/src
  //   await extract(zipPath, { dir: sourceFolder });
  //
  //   await fse.copy('assets/catchthecat/main.cpp', sourceFolder + '/main.cpp', {
  //     overwrite: true,
  //   });
  //   await fse.copy(
  //     'assets/catchthecat/CMakeLists.txt',
  //     sourceFolder + '/CMakeLists.txt',
  //     {
  //       overwrite: true,
  //     },
  //   );
  //   await fse.copy(
  //     'assets/catchthecat/simulator.h',
  //     sourceFolder + '/simulator.h',
  //     {
  //       overwrite: true,
  //     },
  //   );
  //   await fse.copy('assets/catchthecat/IAgent.h', sourceFolder + '/IAgent.h', {
  //     overwrite: true,
  //   });
  //   const outs: TerminalDto[] = [];
  //   // compile the users code
  //   outs.push(
  //     await this.runCommand(
  //       'cmake -S ' + sourceFolder + ' -B ' + sourceFolder + '/build',
  //     ),
  //   );
  //   outs.push(
  //     await this.runCommand('cmake --build ' + sourceFolder + '/build'),
  //   );
  //   // store the compiled code in the user's folder username/
  //   try {
  //     await fse.copy(
  //       sourceFolder + '/build/StudentSimulation',
  //       sourceFolder + '/../StudentSimulation',
  //       { overwrite: true },
  //     );
  //   } catch (err) {
  //     throw new UnprocessableEntityException(
  //       'Server failed to compile code. ',
  //       JSON.stringify(outs),
  //     );
  //   }
  //
  //   return outs;
  // }

  randomMapSide(): number {
    const min = 0;
    const max = 2;
    const rand = Math.floor(Math.random() * (max - min + 1) + min);
    return rand * 4 + 9;
  }

  randomBlock(n: number) {
    const min = n * 0.5;
    const max = n / 0.5;
    const rand = Math.floor(Math.random() * (max - min + 1) + min);
    return rand;
  }

  randomPoint(n: number): Point2D {
    const min = 0;
    const max = n - 1;
    const randX = Math.floor(Math.random() * (max - min + 1) + min);
    const randY = Math.floor(Math.random() * (max - min + 1) + min);
    return { x: randX, y: randY };
  }

  generateInitialMap(): string {
    const sideSize = this.randomMapSide();
    const blocks = this.randomBlock(sideSize);
    const sideOver2 = Math.floor(sideSize / 2);
    const level: string[][] = [];
    const center: Point2D = { x: sideOver2, y: sideOver2 };
    for (let line = 0; line < sideSize; line++) {
      level.push([]);
      for (let col = 0; col < sideSize; col++) {
        level[line].push('.');
      }
    }
    for (let i = 0; i < blocks; i++) {
      const point = this.randomPoint(sideSize);
      if (
        (Math.abs(point.x - center.x) <= 2 &&
          Math.abs(point.y - center.y) <= 2) ||
        level[point.y][point.x] === '#'
      )
        i--;
      else level[point.y][point.x] = '#';
    }
    let result = 'CAT ' + sideSize + ' 0 0\n';
    for (let line = 0; line < sideSize; line++) {
      if (line % 2 === 1) result += ' ';
      for (let col = 0; col < sideSize; col++) {
        if (col === center.x && line === center.y) result += 'C';
        else result += level[line][col];
        if (col < sideSize - 1) result += ' ';
      }
      result += '\n';
    }
    return result;
  }

  async runMatch(
    cat: CompetitionSubmissionEntity,
    catcher: CompetitionSubmissionEntity,
    initialMap: string,
    competition: CompetitionRunEntity,
  ): Promise<CompetitionMatchEntity> {
    const match = this.matchRepository.create();
    match.p1submission = cat;
    match.p2submission = catcher;
    match.p1Points = 0;
    match.p2Points = 0;
    match.p1Turns = 0;
    match.p2Turns = 0;
    match.run = competition;

    let level = new LevelMap(initialMap);

    const levelsize = level.size;
    match.logs = 'Cat: ' + match.p1submission.user.username + '\n';
    match.logs += 'Catcher: ' + match.p2submission.user.username + '\n';
    match.logs += 'InitialMap: \n';
    match.logs += level.board;
    match.logs += '\n';

    while (level.turn === Turn.CAT || level.turn === Turn.CATCHER) {
      match.logs += level.turn + ': ';
      if (level.turn === Turn.CAT)
        match.logs += match.p1submission.user.username + '\n';
      else if (level.turn === Turn.CATCHER)
        match.logs += match.p2submission.user.username + '\n';

      // call the turn
      const result = await this.runCommand(
        "echo '" +
          level.print() +
          "' | " +
          process.cwd() +
          '/submissions/' +
          cat.user.username +
          '/StudentSimulation',
        false,
        10000,
      );
      if (result.stderr) {
        level.turn = Turn[(level.turn + 'ERROR') as keyof typeof Turn];
        match.logs += (result.stderr as any).stdout;
        break;
      }
      // extract output
      level = new LevelMap(result.stdout);
      if (level.turn === Turn.CATCHER) {
        match.p1Turns++;
        match.p1Points -= level.time / 1000000;
      } else if (level.turn === Turn.CAT) {
        match.p2Turns++;
        match.p2Points -= level.time / 1000000;
      } else if (
        level.turn === Turn.CATCHERERROR ||
        level.turn === Turn.CATERROR
      ) {
        match.logs += result.stdout;
        break;
      }

      match.logs += result.stdout;
      match.logs += '\n';
    }

    if (level.turn === Turn.CATCHERWIN) {
      match.winner = CompetitionWinner.Player2;
      match.p2Points += levelsize * levelsize - match.p2Turns;
      match.p1Points += match.p1Turns;
    } else if (level.turn === Turn.CATWIN) {
      match.winner = CompetitionWinner.Player1;
      match.p1Points += levelsize * levelsize - match.p1Turns;
      match.p2Points += match.p2Turns;
    } else if (level.turn === Turn.CATCHERERROR) {
      match.winner = CompetitionWinner.Player1;
      match.p1Points += levelsize * levelsize - match.p1Turns;
    } else if (level.turn === Turn.CATERROR) {
      match.winner = CompetitionWinner.Player2;
      match.p2Points += levelsize * levelsize - match.p2Turns;
    }

    // save the match
    match.run = competition;
    return await this.matchRepository.save(match);
  }

  // async runCatCompetition(): Promise<void> {
  //   // todo: wrap inside a transaction to avoid starting a competition while another is running
  //
  //   //todo: uncomment this
  //   // if (lastCompetition && lastCompetition.state == CompetitionRunState.RUNNING)
  //   //   throw new ConflictException('There is already a competition running');
  //   let competition = this.runRepository.create();
  //   competition.state = CompetitionRunState.RUNNING;
  //   competition = await this.runRepository.save(competition);
  //
  //   try {
  //     // todo: optimize this query
  //     const allUsers = await this.userService.find({ select: { id: true } });
  //     const lastSubmissions = (
  //       await Promise.all(
  //         allUsers.map(async (user) => {
  //           return await this.submissionRepository.findOne({
  //             where: { user: { id: user.id } },
  //             relations: { user: true },
  //             order: { updatedAt: 'DESC' },
  //           });
  //         }),
  //       )
  //     ).filter((submission) => submission !== null);
  //
  //     // compile all submissions
  //     console.log('preparing all submissions');
  //     for (const submission of lastSubmissions) {
  //       try {
  //         await this.prepareLastUserSubmission(submission.user);
  //       } catch (err) {
  //         console.log(err);
  //       }
  //     }
  //
  //     // create 100 boards
  //     // run 100 matches for every combination of 2 submissions
  //     for (let m = 0; m < 1; m++) {
  //       const initialMap = this.generateInitialMap();
  //       for (const cat of lastSubmissions) {
  //         for (const catcher of lastSubmissions) {
  //           // if (catUser == catcherUser) continue; // todo: add this again
  //           const match = await this.runMatch(
  //             cat,
  //             catcher,
  //             initialMap,
  //             competition,
  //           );
  //
  //           // find submission report. if dont exists, create it
  //           // CAT
  //           let catReport: CompetitionRunSubmissionReportEntity =
  //             await this.submissionReportRepository.findOne({
  //               where: {
  //                 run: { id: competition.id },
  //                 submission: { id: cat.id },
  //               },
  //             });
  //           if (!catReport) {
  //             catReport = this.submissionReportRepository.create();
  //             catReport.run = competition;
  //             catReport.submission = cat;
  //             catReport.winsAsP1 = 0;
  //             catReport.winsAsP2 = 0;
  //             catReport.pointsAsP1 = 0;
  //             catReport.pointsAsP2 = 0;
  //             catReport.totalPoints = 0;
  //             catReport = await this.submissionReportRepository.save(catReport);
  //           }
  //           catReport.totalPoints += match.p1Points;
  //           catReport.pointsAsP1 += match.p1Points;
  //           if (match.winner === CompetitionWinner.Player1)
  //             catReport.winsAsP1++;
  //           await this.submissionReportRepository.save(catReport);
  //
  //           // CATCHER
  //           let catcherReport: CompetitionRunSubmissionReportEntity =
  //             await this.submissionReportRepository.findOne({
  //               where: {
  //                 run: { id: competition.id },
  //                 submission: { id: catcher.id },
  //               },
  //             });
  //           if (!catcherReport) {
  //             catcherReport = this.submissionReportRepository.create();
  //             catcherReport.run = competition;
  //             catcherReport.submission = catcher;
  //             catcherReport.winsAsP1 = 0;
  //             catcherReport.winsAsP2 = 0;
  //             catcherReport.pointsAsP1 = 0;
  //             catcherReport.pointsAsP2 = 0;
  //             catcherReport.totalPoints = 0;
  //             catcherReport =
  //               await this.submissionReportRepository.save(catcherReport);
  //           }
  //           catcherReport.totalPoints += match.p2Points;
  //           catcherReport.pointsAsP2 += match.p2Points;
  //           if (match.winner === CompetitionWinner.Player2)
  //             catcherReport.winsAsP2++;
  //           await this.submissionReportRepository.save(catcherReport);
  //         }
  //       }
  //     }
  //
  //     await this.runRepository.update(
  //       { id: competition.id },
  //       {
  //         state: CompetitionRunState.FINISHED,
  //       },
  //     );
  //     competition = await this.runRepository.findOne({
  //       where: { id: competition.id },
  //     });
  //     competition.reports = await this.submissionReportRepository.find({
  //       where: { run: { id: competition.id } },
  //     });
  //
  //     competition.state = CompetitionRunState.FAILED;
  //     competition = await this.runRepository.save(competition);
  //   } catch (e) {}
  // }

  async listChessAgents(): Promise<string[]> {
    // find users who have submitted chess agents
    const users = this.userService.find({
      where: { competitionSubmissions: { gameType: CompetitionGame.Chess } },
    });
    // return their usernames
    const users_1 = await users;
    return users_1.map((user) => user.username);
  }

  async prepareLastChessSubmission(user: UserEntity): Promise<TerminalDto[]> {
    const submission = await this.submissionRepository.findOne({
      where: { user: { id: user.id }, gameType: CompetitionGame.Chess },
      order: { createdAt: 'DESC' },
    });
    const userFolder = process.cwd() + '/chessSubmissions/' + user.username;
    const srcFolder = userFolder + '/git';
    const botFolder = srcFolder + '/chess-bot';
    const unzipFolder = userFolder + '/unzip';
    const zipFilePath = userFolder + '/' + this.getDate() + '.zip';
    const buildFolder = userFolder + '/build';

    // name the file as datetime.zip
    // store the zip file in the user's folder username/zips
    await fsp.mkdir(userFolder, { recursive: true });
    await fsp.writeFile(zipFilePath, submission.sourceCodeZip);

    // download the chess engine
    const chessEngineGitUrl =
      'https://github.com/InfiniBrains/chess-competition.git';
    // if chess engine folder is there, just run git pull, otherwise just run git clone
    if (!fse.existsSync(srcFolder)) {
      const gitReponse = await simpleGit().clone(chessEngineGitUrl, srcFolder);
      console.log(gitReponse);
    } else {
      await simpleGit(srcFolder).reset(ResetMode.HARD)
        .clean(
          CleanOptions.FORCE +
            CleanOptions.RECURSIVE +
            CleanOptions.IGNORED_INCLUDED +
            CleanOptions.DRY_RUN,
        ).pull();
    }

    // clear the contents of bot folder
    await fsp.rm(botFolder, { recursive: true, force: true });
    await fsp.mkdir(botFolder, { recursive: true });

    // unzip the zip contents into the bot folder
    await decompress(zipFilePath, botFolder);

    // generate cmake project folder and build
    await this.runCommandSpawn(
      'cmake -S' +
        srcFolder +
        ' -B' +
        buildFolder +
        ' -DCMAKE_BUILD_TYPE=MinSizeRel -DCHESS_VALIDATOR_ONLY=ON',
    );
    await this.runCommandSpawn(
      'cmake --build ' + buildFolder + ' --target chesscli --config MinSizeRel',
    );

    // todo: compress the executable built to save space

    // get the bytes of the executable
    submission.executable = await fsp.readFile(buildFolder + '/chesscli');
    await this.submissionRepository.save(submission);
    return;
  }

  async RequestChessMove(data: ChessMoveRequestDto): Promise<string> {
    // find last submission of the user
    const submission = await this.submissionRepository.findOne({
      where: {
        user: { username: data.username },
        gameType: CompetitionGame.Chess,
      },
      order: { createdAt: 'DESC' },
    });

    if (!submission)
      throw new UnprocessableEntityException(
        'No submission found for this user',
      );

    const userFolder = process.cwd() + '/chessSubmissions/' + data.username;
    const buildFolder = userFolder + '/build';
    const executablePath = buildFolder + '/chesscli';

    if (!fse.existsSync(executablePath)) {
      // create build folder
      await fsp.mkdir(buildFolder, { recursive: true });
      await fsp.writeFile(executablePath, submission.executable);
      await this.runCommandSpawn('chmod +x ' + executablePath);
    }
    const output = await ExecuteCommand({
      command: executablePath,
      stdin: data.fen,
      timeout: 10000,
    });
    if (output.stderr)
      throw new UnprocessableEntityException(
        'Error running the executable: ' + output.stderr,
      );
    return output.stdout;
  }

  async RunChessMatch(
    ChessMatchRequestDto: ChessMatchRequestDto,
    competitionRun: CompetitionRunEntity = null,
  ): Promise<{ result: ChessMatchResultDto; match: CompetitionMatchEntity }> {
    const usernames = [
      ChessMatchRequestDto.player1username,
      ChessMatchRequestDto.player2username,
    ];
    
    console.log("Match request: ", usernames);

    // find users to be able to get their elo
    // todo: move elo to user profile
    const users = await Promise.all(
      usernames.map(async (username) => {
        return await this.userService.findOne({
          where: { username: username },
        });
      }),
    );

    // users should be found
    for (let i = 0; i < users.length; i++) {
      if (!users[i])
        throw new UnprocessableEntityException(
          'User ' + usernames[i] + ' not found',
        );
    }

    // find the last submission from both players
    const submissions: CompetitionSubmissionEntity[] = await Promise.all(
      usernames.map(async (username) => {
        return await this.submissionRepository.findOne({
          where: {
            user: { username: username },
            gameType: CompetitionGame.Chess,
            executable: Not(IsNull()),
          },
          order: { createdAt: 'DESC' },
          relations: { user: true },
        });
      }),
    );

    // report error if any of the submissions is not found
    for (let i = 0; i < submissions.length; i++)
      if (!submissions[i]) {
        let msg = 'No submission found for player ' + usernames[i];
        console.log(msg);
        throw new UnprocessableEntityException(msg);
      }

    // place both executables in their respective folders
    // path for the executable and folder
    const executableFolder: string[] = usernames.map((username) => {
      return process.cwd() + '/chessSubmissions/' + username + '/build';
    });
    const executablePath: string[] = usernames.map((username) => {
      return (
        process.cwd() + '/chessSubmissions/' + username + '/build/chesscli'
      );
    });

    // create folder if doesn't exist
    for (let i = 0; i < executableFolder.length; i++) {
      if (!fse.existsSync(executableFolder[i]))
        await fsp.mkdir(executableFolder[i], { recursive: true });
    }

    // delete the old files and write the new ones, set the permissions
    for (let i = 0; i < executablePath.length; i++) {
      await fsp.rm(executablePath[i], { recursive: true, force: true });
      await fsp.writeFile(executablePath[i], submissions[i].executable);
      await this.runCommandSpawn('chmod +x ' + executablePath[i]);
    }

    // the board management settings
    const board = new Chess();
    let userIdx: number = 0;
    const result: ChessMatchResultDto = {
      id: '',
      players: usernames,
      winner: '',
      draw: false,
      result: ChessGameResult.NONE,
      reason: ChessGameResultReason.NONE,
      cpuTime: [0, 0],
      eloChange: [0, 0],
      elo: [0, 0],
      finalFen: board.fen(),
      moves: [],
      createdAt: new Date(),
    };

    // run the match while they are both reporting moves and accumulate all the moves into an array of strings
    while (true) {
      const fen = board.fen();
      let move: ExecuteCommandResult;
      try {
        move = await ExecuteCommand({
          command: executablePath[userIdx],
          stdin: fen,
          timeout: 10000,
        });
      } catch (e) {
        console.error(e);
        move = null;
      }

      // if the exe breaks, the other player wins
      if (!move || !move.stdout || move.stderr) {
        // username of the failed player
        console.error('user', usernames[userIdx]);
        console.error('command', executablePath[userIdx]);
        console.error('fen', fen);
        if (move) console.error('stdout', move.stdout);

        result.winner = usernames[1 - userIdx];
        result.result = ChessGameResult.GAME_OVER;
        result.reason = ChessGameResultReason.INVALID_MOVE;
        result.draw = false;
        result.finalFen = board.fen();
        break;
      }

      result.cpuTime[userIdx] += move.duration / 1e9;

      // remove empty spaces
      move.stdout = move.stdout.replace(/\s/g, '');

      // check if the move is valid
      let moveResult: Move;
      try {
        moveResult = board.move(move.stdout);
      } catch (e) {
        moveResult = null;
      }

      // if the move is invalid, the other player wins
      if (!moveResult) {
        result.draw = false;
        result.result = ChessGameResult.GAME_OVER;
        result.reason = ChessGameResultReason.INVALID_MOVE;
        result.winner = usernames[1 - userIdx];
        result.finalFen = board.fen();
        break;
      }

      // add the move to the moves array
      result.moves.push(moveResult.lan);

      // isGameOver Returns true if the game has ended via checkmate, stalemate, draw, threefold repetition, or insufficient material. Otherwise, returns false.

      if (board.isGameOver()) {
        // checkmate
        if (board.isCheckmate()) {
          result.draw = false;
          result.winner = usernames[userIdx];
          result.reason = ChessGameResultReason.CHECKMATE;
          result.result = ChessGameResult.GAME_OVER;
          result.finalFen = board.fen();
          break;
        }

        // stalemate
        else if (board.isStalemate()) {
          result.draw = true;
          result.result = ChessGameResult.DRAW;
          result.reason = ChessGameResultReason.STALEMATE;
          result.finalFen = board.fen();
          break;
        }

        // threefold repetition
        else if (board.isThreefoldRepetition()) {
          result.draw = true;
          result.result = ChessGameResult.DRAW;
          result.reason = ChessGameResultReason.THREEFOLD_REPETITION;
          result.finalFen = board.fen();
          break;
        }

        // insufficient material
        else if (board.isInsufficientMaterial()) {
          result.draw = true;
          result.result = ChessGameResult.DRAW;
          result.reason = ChessGameResultReason.INSUFFICIENT_MATERIAL;
          result.finalFen = board.fen();
          break;
        }

        // isdraw Returns true or false if the game is drawn (50-move rule or insufficient material).
        // insufficient material is already checked so it will be 50-move rule
        else if (board.isDraw()) {
          result.draw = true;
          result.result = ChessGameResult.DRAW;
          result.reason = ChessGameResultReason.FIFTY_MOVE_RULE;
          result.finalFen = board.fen();
          break;
        } else {
          // this should never happen
          result.result = ChessGameResult.GAME_OVER;
          result.reason = ChessGameResultReason.NONE;
          result.draw = false;
          result.finalFen = board.fen();
          break;
        }
      }

      // draw by 5000 moves
      if (result.moves.length >= 5000) {
        // this should never happen. added for safety reasons
        result.draw = true;
        result.result = ChessGameResult.DRAW;
        result.reason = ChessGameResultReason.FIFTY_MOVE_RULE;
        result.finalFen = board.fen();
        break;
      }

      // switch the user
      userIdx = 1 - userIdx;
    }

    // update the elo of the users
    // todo: @joel check this please!
    if (result.winner) {
      const winnerEntity = users.find(
        (user) => user.username === result.winner,
      );
      const loserEntity = users.find((user) => user.username !== result.winner);
      const newElo = this.calculateNewElo({
        winner: winnerEntity.elo,
        loser: loserEntity.elo,
      });
      // update the elo of the users
      if (result.winner === usernames[0]) {
        result.eloChange[0] = newElo.winner - winnerEntity.elo;
        result.eloChange[1] = newElo.loser - loserEntity.elo;
        result.elo = [newElo.winner, newElo.loser];
      } else if (result.winner === usernames[1]) {
        result.eloChange[1] = newElo.winner - winnerEntity.elo;
        result.eloChange[0] = newElo.loser - loserEntity.elo;
        result.elo = [newElo.loser, newElo.winner];
      }
      await this.userService.updateOneTypeorm(winnerEntity.id, {
        elo: newElo.winner,
      });
      await this.userService.updateOneTypeorm(loserEntity.id, {
        elo: newElo.loser,
      });
    }

    // create a match entity and save it
    let match = this.matchRepository.create();
    let playerWinner: CompetitionWinner | null;
    if (result.winner === usernames[0])
      playerWinner = CompetitionWinner.Player1;
    else if (result.winner === usernames[1])
      playerWinner = CompetitionWinner.Player2;
    else playerWinner = null;

    match = {
      ...match,
      p1submission: submissions[0],
      p2submission: submissions[1],
      p1Points: result.eloChange[0], // todo: take time in consideration
      p2Points: result.eloChange[1],
      p1Turns: 0,
      p2Turns: 0,
      run: competitionRun,
      winner: playerWinner, // todo: is it better to use a reference to the submission? the user? the username? or just use player1 and player2? or use boolen to store firstPlayerWon?
      logs: JSON.stringify(result),
      lastState: result.finalFen,
    };

    // save the match

    const matchSaved = await this.matchRepository.save(match);
    result.id = matchSaved.id;
    return { result: result, match: matchSaved };
  }

  // todo: @joel check this please!
  calculateNewElo(data: { winner: number; loser: number }): {
    winner: number;
    loser: number;
  } {
    const [winner, loser] = [data.winner, data.loser];
    // Constants
    const K = 32;

    // Calculate expected results
    const expectedWinner = 1 / (1 + Math.pow(10, (loser - winner) / 400));
    const expectedLoser = 1 / (1 + Math.pow(10, (winner - loser) / 400));

    // Update ratings
    const newWinner = winner + K * (1 - expectedWinner);
    const newLoser = loser + K * (0 - expectedLoser);

    return { winner: newWinner, loser: newLoser };
  }

  async findMatchesByCriteria(
    criteria: FindManyOptions<CompetitionMatchEntity>,
  ): Promise<CompetitionMatchEntity[]> {
    return await this.matchRepository.find(criteria);
  }

  async findMatchById(id: string): Promise<CompetitionMatchEntity> {
    return await this.matchRepository.findOne({ where: { id } });
  }

  async getLeaderboard(): Promise<ChessLeaderboardResponseDto> {
    const users = await this.userService.find({
      select: {
        username: true,
        elo: true,
      },
      order: {
        elo: 'DESC',
      },
      take: 5,
    });

    return users;
  }

  async runChessCompetition() {
    // check if the last the competition is running
    const lastCompetition = await this.runRepository.findOne({
      where: { gameType: CompetitionGame.Chess },
      order: { updatedAt: 'DESC' },
    });
    if (
      lastCompetition &&
      lastCompetition.gameType == CompetitionGame.Chess &&
      lastCompetition.state == CompetitionRunState.RUNNING
    ) {
      // if last competition running is from less than 1h ago then throw error
      // todo: fix 4 hours displacement between database and server
      const previousTime = moment(lastCompetition.updatedAt).subtract(4, 'hours');
      // less than one hour
      if(moment().diff(previousTime) < 1000 * 60 * 60) 
        throw new ConflictException('There is already a competition running');
      else // unstuck the competition 
        await this.deleteStuckCompetition(CompetitionGame.Chess);
    }
    
    // create a new competition
    let competition = this.runRepository.create();
    competition.state = CompetitionRunState.RUNNING;
    competition.gameType = CompetitionGame.Chess;
    competition = await this.runRepository.save(competition);

    // try to run the competition
    try {
      // list all user ids
      const users = await this.userService.find({
        select: {
          id: true,
          username: true,
        },
      });

      // find last valid submission for each user and remove if it is null
      let submissions: CompetitionSubmissionEntity[] = await Promise.all(
        users.map(async (user) => {
          return await this.submissionRepository.findOne({
            where: {
              user: { id: user.id },
              gameType: CompetitionGame.Chess,
              executable: Not(IsNull()),
            },
            relations: { user: true },
            order: { createdAt: 'DESC' },
          });
        }),
      );
      // remove null submissions
      submissions = submissions.filter((s) => s !== null);

      // run N*N matches between all users
      for (let i = 0; i < submissions.length; i++) {
        for (let j = 0; j < submissions.length; j++) {
          const p1 = submissions[i];
          const p2 = submissions[j];
          if (!p1 || !p2 || p1.id === p2.id) continue;
          // print the users playing
          console.log(
            `Progress: ${((100 * (i * submissions.length + j)) / (submissions.length * submissions.length)).toFixed(2)}. Playing: ${submissions[i].user.username} vs ${submissions[j].user.username}`,
          );
          const match = await this.RunChessMatch(
            {
              player1username: p1.user.username,
              player2username: p2.user.username,
            },
            competition,
          );
          // update / create reports
          await this.updateReport(match.match, competition);
        }
      }

      // update competition run state to finished
      await this.runRepository.update(competition.id, {
        state: CompetitionRunState.FINISHED,
      });
      console.log('Competition finished');
    }
    catch (e) {
      await this.runRepository.update(competition.id, {
        state: CompetitionRunState.FAILED,
      });
      // print error
      console.error(e);
      throw e;
    }
  }

  async deleteStuckCompetition(gameType: CompetitionGame): Promise<void> {
    // get last competition
    const competition = await this.runRepository.findOne({
      where: { gameType, state: CompetitionRunState.RUNNING },
      order: { updatedAt: 'DESC' },
    });
    if (!competition) return;
    
    // go over all the matches and delete them
    await this.matchRepository.delete({
      run: { id: competition.id },
    });
    
    // delete the reports
    await this.submissionReportRepository.delete({
      run: { id: competition.id },
    });

    // delete the competition
    await this.runRepository.delete({ id: competition.id });
  }
  
  async getLatestChessCompetitionReport(): Promise<
    CompetitionRunSubmissionReportDto[]
  > {
    // get the latest finished competition
    const competition = await this.runRepository.findOne({
      where: {
        gameType: CompetitionGame.Chess,
        state: CompetitionRunState.FINISHED,
      },
      order: { updatedAt: 'DESC' },
    });
    if (!competition) return [];
    // get all the reports for that competition with usernames
    return await this.submissionReportRepository.find({
      where: { run: { id: competition.id } },
      relations: { user: true },
      order: { totalWins: 'DESC' },
    });
  }
  async updateReport(match: CompetitionMatchEntity, run: CompetitionRunEntity) {
    const winner = match.winner;

    // find the report for P1 and create one if it doesn't exist
    let reportP1 = await this.submissionReportRepository.findOne({
      where: {
        user: { username: match.p1submission.user.username },
        run: { id: run.id },
      },
    });

    // if there is no report for P1, create one
    if (!reportP1) {
      reportP1 = this.submissionReportRepository.create();
      reportP1.user = match.p1submission.user;
      reportP1.run = run;
      reportP1.submission = match.p1submission;
      reportP1.totalWins = 0;
      reportP1.winsAsP1 = 0;
      reportP1.winsAsP2 = 0;
      reportP1.pointsAsP1 = 0;
      reportP1.pointsAsP2 = 0;
      reportP1.totalPoints = 0;
    }
    // update the report for P1
    reportP1.winsAsP1 += winner == CompetitionWinner.Player1 ? 1 : 0;
    reportP1.totalWins += winner == CompetitionWinner.Player1 ? 1 : 0;
    reportP1 = await this.submissionReportRepository.save(reportP1);

    // find the report for P2 and create one if it doesn't exist
    let reportP2 = await this.submissionReportRepository.findOne({
      where: {
        user: { username: match.p2submission.user.username },
        run: { id: run.id },
      },
    });

    // if there is no report for P2, create one
    if (!reportP2) {
      reportP2 = this.submissionReportRepository.create();
      reportP2.user = match.p2submission.user;
      reportP2.run = run;
      reportP2.submission = match.p2submission;
      reportP2.totalWins = 0;
      reportP2.winsAsP1 = 0;
      reportP2.winsAsP2 = 0;
      reportP2.pointsAsP1 = 0;
      reportP2.pointsAsP2 = 0;
      reportP2.totalPoints = 0;
    }
    // update the report for P2
    reportP2.winsAsP2 += winner === CompetitionWinner.Player2 ? 1 : 0;
    reportP2.totalWins += winner === CompetitionWinner.Player2 ? 1 : 0;
    reportP2 = await this.submissionReportRepository.save(reportP2);
  }
}
