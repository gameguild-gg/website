import {Injectable, NotImplementedException, UnprocessableEntityException,} from '@nestjs/common';
import {AuthService} from '../auth/auth.service';
import {TerminalDto} from './dtos/terminal.dto';
import * as util from 'util';
import {InjectRepository} from '@nestjs/typeorm';
import {Repository} from 'typeorm';
import {CompetitionGame, CompetitionSubmissionEntity} from './entities/competition.submission.entity';
import {CompetitionRunEntity, CompetitionRunState,} from './entities/competition.run.entity';
import {CompetitionMatchEntity, CompetitionWinner,} from './entities/competition.match.entity';
import {promises as fsp} from 'fs';
import fs from 'fs';
import * as fse from 'fs-extra';
import {UserService} from '../user/user.service';
import {LinqRepository} from 'typeorm-linq-repository';
import {CompetitionRunSubmissionReportEntity} from './entities/competition.run.submission.report.entity';
import {UserEntity} from "../user/entities";
import {simpleGit} from 'simple-git';
import {CleanOptions} from "simple-git";
import * as process from "process";
import extract from "extract-zip";
import * as decompress from "decompress";
import {ChessMoveRequestDto} from "./dtos/chess-move-request.dto";
const execShPromise = require("exec-sh").promise;

const exec = util.promisify(require('child_process').exec);

import ExecuteCommand from '../common/execute-command';

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
  }

  getDate(): string {
    return new Date().toISOString();
  }

  async storeSubmission(data: { user: UserEntity; file: Buffer, gameType: CompetitionGame }) {
    const submission = this.submissionRepository.save({
      user: data.user,
      sourceCodeZip: data.file,
      gameType: data.gameType
    });
    return submission;
  }

  async runCommandSpawn(command: string): Promise<{stdout: string, stderr: string}> {
    return await execShPromise(command, {maxBuffer: 1024 * 1024 * 50, });
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

  async prepareLastUserSubmission(user: UserEntity): Promise<TerminalDto[]> {
    const submission = await this.submissionRepository.findOne({
      where: { user: { id: user.id } },
      order: { createdAt: 'DESC' },
    });
    const userFolder = process.cwd() + '/submissions/' + user.username;
    await fsp.rm(userFolder, { recursive: true, force: true });

    // rename the file as datetime.zip
    // store the zip file in the user's folder username/zips
    await fsp.mkdir('submissions/' + user.username + '/zips', {
      recursive: true,
    });
    const zipPath =
      'submissions/' + user.username + '/zips/' + this.getDate() + '.zip';
    await fsp.writeFile(zipPath, submission.sourceCodeZip);
    const sourceFolder =
      process.cwd() + '/submissions/' + user.username + '/src';

    // clear the user's folder username/src
    await fsp.rm(sourceFolder, { recursive: true, force: true });
    await fsp.mkdir(sourceFolder, { recursive: true });

    // unzip the file into the user's folder username/src
    await extract(zipPath, { dir: sourceFolder });

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
      'assets/catchthecat/simulator.h',
      sourceFolder + '/simulator.h',
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
      await this.runCommand(
        'cmake -S ' + sourceFolder + ' -B ' + sourceFolder + '/build',
      ),
    );
    outs.push(
      await this.runCommand('cmake --build ' + sourceFolder + '/build'),
    );
    // store the compiled code in the user's folder username/
    try {
      await fse.copy(
        sourceFolder + '/build/StudentSimulation',
        sourceFolder + '/../StudentSimulation',
        { overwrite: true },
      );
    } catch (err) {
      throw new UnprocessableEntityException(
        'Server failed to compile code. ',
        JSON.stringify(outs),
      );
    }

    return outs;
  }

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
      if (level.turn === Turn.CAT) match.logs += match.p1submission.user.username + '\n';
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

  async run(): Promise<void> {
    // todo: wrap inside a transaction to avoid starting a competition while another is running
    // const lastCompetition = await this.runRepository.findOne({
    //   where: {},
    //   order: { updatedAt: 'DESC' },
    // });
    //todo: uncomment this
    // if (lastCompetition && lastCompetition.state == CompetitionRunState.RUNNING)
    //   throw new ConflictException('There is already a competition running');
    let competition = this.runRepository.create();
    competition.state = CompetitionRunState.RUNNING;
    competition = await this.runRepository.save(competition);

    try {
      // todo: optimize this query
      // get the last submission of each user
      // const users = await this.userService.repositoryLinq
      //   .getAll()
      //   .groupBy((u) => u.id)
      //   .include((u) => u.competitionSubmissions)
      //   .thenGroupBy((c) => c.id)
      //   .orderByDescending((c) => c.updatedAt)
      //   .take(1);
      // return users;

      const allUsers = await this.userService.find({ select: ['id'] });
      const lastSubmissions = (
        await Promise.all(
          allUsers.map(async (user) => {
            return await this.submissionRepository.findOne({
              where: { user: { id: user.id } },
              relations: ['user'],
              order: { updatedAt: 'DESC' },
            });
          }),
        )
      ).filter((submission) => submission !== null);

      // compile all submissions
      console.log('preparing all submissions');
      for (const submission of lastSubmissions) {
        try {
          await this.prepareLastUserSubmission(submission.user);
        } catch (err) {
          console.log(err);
        }
      }

      // create 100 boards
      // run 100 matches for every combination of 2 submissions
      for (let m = 0; m < 1; m++) {
        const initialMap = this.generateInitialMap();
        for (const cat of lastSubmissions) {
          for (const catcher of lastSubmissions) {
            // if (catUser == catcherUser) continue; // todo: add this again
            const match = await this.runMatch(
              cat,
              catcher,
              initialMap,
              competition,
            );

            // find submission report. if dont exists, create it
            // CAT
            let catReport: CompetitionRunSubmissionReportEntity =
              await this.submissionReportRepository.findOne({
                where: {
                  run: { id: competition.id },
                  submission: { id: cat.id },
                },
              });
            if (!catReport) {
              catReport = this.submissionReportRepository.create();
              catReport.run = competition;
              catReport.submission = cat;
              catReport.winsAsP1 = 0;
              catReport.winsAsP2 = 0;
              catReport.p1Points = 0;
              catReport.p2Points = 0;
              catReport.totalPoints = 0;
              catReport = await this.submissionReportRepository.save(catReport);
            }
            catReport.totalPoints += match.p1Points;
            catReport.p1Points += match.p1Points;
            if (match.winner === CompetitionWinner.Player1) catReport.winsAsP1++;
            await this.submissionReportRepository.save(catReport);

            // CATCHER
            let catcherReport: CompetitionRunSubmissionReportEntity =
              await this.submissionReportRepository.findOne({
                where: {
                  run: { id: competition.id },
                  submission: { id: catcher.id },
                },
              });
            if (!catcherReport) {
              catcherReport = this.submissionReportRepository.create();
              catcherReport.run = competition;
              catcherReport.submission = catcher;
              catcherReport.winsAsP1 = 0;
              catcherReport.winsAsP2 = 0;
              catcherReport.p1Points = 0;
              catcherReport.p2Points = 0;
              catcherReport.totalPoints = 0;
              catcherReport = await this.submissionReportRepository.save(
                catcherReport,
              );
            }
            catcherReport.totalPoints += match.p2Points;
            catcherReport.p2Points += match.p2Points;
            if (match.winner === CompetitionWinner.Player2)
              catcherReport.winsAsP2++;
            await this.submissionReportRepository.save(catcherReport);
          }
        }
      }

      await this.runRepository.update(
        { id: competition.id },
        {
          state: CompetitionRunState.FINISHED,
        },
      );
      competition = await this.runRepository.findOne({
        where: { id: competition.id },
      });
      competition.reports = await this.submissionReportRepository.find({ where: { run: { id: competition.id } }});

      competition.state = CompetitionRunState.FAILED;
      competition = await this.runRepository.save(competition);
    } catch (e){
      
    }
  }

  async listChessAgents(): Promise<string[]> {
    // find users who have submitted chess agents
    let users = this.userService.find({where: {competitionSubmissions: {gameType: CompetitionGame.Chess}}});
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
    const srcFolder = userFolder + '/git'
    const botFolder = srcFolder + '/chess-bot'
    const unzipFolder = userFolder + '/unzip'
    const zipFilePath = userFolder + '/' + this.getDate() + '.zip';
    const buildFolder = userFolder + '/build';
    
    // name the file as datetime.zip
    // store the zip file in the user's folder username/zips
    await fsp.mkdir(userFolder, {recursive: true});
    await fsp.writeFile(zipFilePath, submission.sourceCodeZip);

    // download the chess engine
    const chessEngineGitUrl = 'https://github.com/InfiniBrains/chess-competition.git';
    // if chess engine folder is there, just run git pull, otherwise just run git clone 
    if(!fse.existsSync(srcFolder)) {
      let gitReponse = await simpleGit().clone(chessEngineGitUrl, srcFolder);
      console.log(gitReponse);
    } else {
      await simpleGit(srcFolder).pull().clean(CleanOptions.FORCE + CleanOptions.RECURSIVE + CleanOptions.IGNORED_INCLUDED);
    }
    
    // clear the contents of bot folder
    await fsp.rm(botFolder, { recursive: true, force: true });
    await fsp.mkdir(botFolder, { recursive: true });
    
    // unzip the zip contents into the bot folder
    await decompress(zipFilePath, botFolder);
    
    // generate cmake project folder and build
    await this.runCommandSpawn('cmake -S' + srcFolder + ' -B' + buildFolder + ' -DCMAKE_BUILD_TYPE=MinSizeRel -DCHESS_VALIDATOR_ONLY=ON');
    await this.runCommandSpawn('cmake --build ' + buildFolder + ' --target chesscli --config MinSizeRel');
    
    // todo: compress the executable built to save space
    
    // get the bytes of the executable
    submission.executable = await fsp.readFile(buildFolder + '/chesscli');
    await this.submissionRepository.save(submission);
    return;
  }

  async RequestChessMove(data: ChessMoveRequestDto): Promise<string> {
    // find last submission of the user
    const submission = await this.submissionRepository.findOne({
      where: { user: { username: data.username }, gameType: CompetitionGame.Chess },
      order: { createdAt: 'DESC' },
    });
    
    if(!submission) throw new UnprocessableEntityException('No submission found for this user');

    const userFolder = process.cwd() + '/chessSubmissions/' + data.username;
    const buildFolder = userFolder + '/build';
    const executablePath = buildFolder + '/chesscli'
    
    if(!fse.existsSync(executablePath)) {
      // create build folder
      await fsp.mkdir(buildFolder, {recursive: true});
      await fsp.writeFile(executablePath, submission.executable);
      await this.runCommandSpawn('chmod +x ' + executablePath);
    }
    let output = await ExecuteCommand({command: executablePath, stdin: data.fen, timeout: 10000});
    if(output.stderr) throw new UnprocessableEntityException('Error running the executable: '+ output.stderr);
    return output.stdout;
  }
}
