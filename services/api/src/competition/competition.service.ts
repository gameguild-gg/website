import {
  ConflictException,
  Injectable,
  UnprocessableEntityException,
} from '@nestjs/common';
import { AuthService } from '../auth/auth.service';
import { TerminalDto } from './dtos/terminal.dto';
import * as util from 'util';
import { InjectRepository } from '@nestjs/typeorm';
import { DataSource, Repository } from 'typeorm';
import { CompetitionSubmissionEntity } from './entities/competition.submission.entity';
import {
  CompetitionRunEntity,
  CompetitionRunState,
} from './entities/competition.run.entity';
import {
  CompetitionMatchEntity,
  CompetitionWinner,
} from './entities/competition.match.entity';
import extract from 'extract-zip';
import fs from 'fs/promises';
import process from 'process';
import * as fse from 'fs-extra';
import { UserService } from '../user/user.service';
import { LinqRepository } from 'typeorm-linq-repository';
import { CompetitionRunSubmissionReportEntity } from './entities/competition.run.submission.report.entity';
import {UserEntity} from "../user/entities";

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

  async storeSubmission(data: { user: UserEntity; file: Buffer }) {
    const submission = this.submissionRepository.save({
      user: data.user,
      sourceCodeZip: data.file,
    });
    return submission;
  }

  async runCommand(
    command: string,
    log = true,
    timeout: number = null,
  ): Promise<TerminalDto> {
    let stdout: string, stderr: string;
    try {
      if (timeout === null) {
        const out = await exec(command);
        stdout = out.stdout;
        stderr = out.stderr;
      } else {
        const out = await exec(command, { timeout: timeout });
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
    await fs.appendFile('log.txt', JSON.stringify(data) + '\n', 'utf8');
  }

  async prepareLastUserSubmission(user: UserEntity): Promise<TerminalDto[]> {
    const submission = await this.submissionRepository.findOne({
      where: { user: { id: user.id } },
      order: { createdAt: 'DESC' },
    });
    const userFolder = process.cwd() + '/submissions/' + user.username;
    await fs.rm(userFolder, { recursive: true, force: true });

    // rename the file as datetime.zip
    // store the zip file in the user's folder username/zips
    await fs.mkdir('submissions/' + user.username + '/zips', {
      recursive: true,
    });
    const zipPath =
      'submissions/' + user.username + '/zips/' + this.getDate() + '.zip';
    await fs.writeFile(zipPath, submission.sourceCodeZip);
    const sourceFolder =
      process.cwd() + '/submissions/' + user.username + '/src';

    // clear the user's folder username/src
    await fs.rm(sourceFolder, { recursive: true, force: true });
    await fs.mkdir(sourceFolder, { recursive: true });

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
    match.cat = cat;
    match.catcher = catcher;
    match.catPoints = 0;
    match.catcherPoints = 0;
    match.catTurns = 0;
    match.catcherTurns = 0;
    match.run = competition;

    let level = new LevelMap(initialMap);

    const levelsize = level.size;
    match.logs = 'Cat: ' + match.cat.user.username + '\n';
    match.logs += 'Catcher: ' + match.catcher.user.username + '\n';
    match.logs += 'InitialMap: \n';
    match.logs += level.board;
    match.logs += '\n';

    while (level.turn === Turn.CAT || level.turn === Turn.CATCHER) {
      match.logs += level.turn + ': ';
      if (level.turn === Turn.CAT) match.logs += match.cat.user.username + '\n';
      else if (level.turn === Turn.CATCHER)
        match.logs += match.catcher.user.username + '\n';

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
        match.catTurns++;
        match.catPoints -= level.time / 1000000;
      } else if (level.turn === Turn.CAT) {
        match.catcherTurns++;
        match.catcherPoints -= level.time / 1000000;
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
      match.winner = CompetitionWinner.CATCHER;
      match.catcherPoints += levelsize * levelsize - match.catcherTurns;
      match.catPoints += match.catTurns;
    } else if (level.turn === Turn.CATWIN) {
      match.winner = CompetitionWinner.CAT;
      match.catPoints += levelsize * levelsize - match.catTurns;
      match.catcherPoints += match.catcherTurns;
    } else if (level.turn === Turn.CATCHERERROR) {
      match.winner = CompetitionWinner.CAT;
      match.catPoints += levelsize * levelsize - match.catTurns;
    } else if (level.turn === Turn.CATERROR) {
      match.winner = CompetitionWinner.CATCHER;
      match.catcherPoints += levelsize * levelsize - match.catcherTurns;
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
              catReport.winsAsCat = 0;
              catReport.winsAsCatcher = 0;
              catReport.catPoints = 0;
              catReport.catcherPoints = 0;
              catReport.totalPoints = 0;
              catReport = await this.submissionReportRepository.save(catReport);
            }
            catReport.totalPoints += match.catPoints;
            catReport.catPoints += match.catPoints;
            if (match.winner === CompetitionWinner.CAT) catReport.winsAsCat++;
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
              catcherReport.winsAsCat = 0;
              catcherReport.winsAsCatcher = 0;
              catcherReport.catPoints = 0;
              catcherReport.catcherPoints = 0;
              catcherReport.totalPoints = 0;
              catcherReport = await this.submissionReportRepository.save(
                catcherReport,
              );
            }
            catcherReport.totalPoints += match.catcherPoints;
            catcherReport.catcherPoints += match.catcherPoints;
            if (match.winner === CompetitionWinner.CATCHER)
              catcherReport.winsAsCatcher++;
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
}
