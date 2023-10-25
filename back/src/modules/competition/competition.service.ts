import { ConflictException, Injectable } from '@nestjs/common';
import { AuthService } from '../auth/auth.service';
import { TerminalDto } from './dtos/terminal.dto';
import * as util from 'util';
import { UserEntity } from '../user/user.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CompetitionSubmissionEntity } from './entities/competition.submission.entity';
import {
  CompetitionRunEntity,
  CompetitionRunState,
} from './entities/competition.run.entity';
import { CompetitionMatchEntity } from './entities/competition.match.entity';
import extract from 'extract-zip';
import fs from 'fs/promises';
import process from 'process';
import * as fse from 'fs-extra';
import { UserService } from '../user/user.service';

const exec = util.promisify(require('child_process').exec);

class Point2D {
  x: number;
  y: number;
}

@Injectable()
export class CompetitionService {
  constructor(
    public authService: AuthService,
    public userService: UserService,
    @InjectRepository(CompetitionSubmissionEntity)
    public submissionRepository: Repository<CompetitionSubmissionEntity>,
    @InjectRepository(CompetitionRunEntity)
    public runRepository: Repository<CompetitionRunEntity>,
    @InjectRepository(CompetitionMatchEntity)
    public matchRepository: Repository<CompetitionMatchEntity>,
  ) {}

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

  async runCommand(command: string, log = true): Promise<TerminalDto> {
    let stdout: string, stderr: string;
    try {
      const out = await exec(command);
      stdout = out.stdout;
      stderr = out.stderr;
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
    // run automated tests
    outs.push(
      await this.runCommand(
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

    return outs;
  }

  randomMapSide(): number {
    const min = 0;
    const max = 6;
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

  async runMatch(cat: string, catcher: string, initialMap: string) {

  }

  async run() {
    // todo: wrap inside a transaction to avoid starting a competition while another is running
    const lastCompetition = await this.runRepository.findOne({
      where: {},
      order: { updatedAt: 'DESC' },
    });
    //todo: uncomment this
    // if (lastCompetition && lastCompetition.state == CompetitionRunState.RUNNING)
    //   throw new ConflictException('There is already a competition running');
    let competition = this.runRepository.create();
    competition.state = CompetitionRunState.RUNNING;
    competition = await this.runRepository.save(competition);

    try {
      // todo: optimize this query
      // get the last submission of each user
      const allUsers = await this.userService.find({ select: ['id'] });
      const lastSubmissions = (
        await Promise.all(
          allUsers.map(async (user) => {
            return await this.submissionRepository.findOne({
              where: { user: { id: user.id } },
              order: { updatedAt: 'DESC' },
            });
          }),
        )
      ).filter((submission) => submission !== null);

      // compile all submissions
      console.log('preparing all submissions');
      let usernames: string[];
      for (const submission of lastSubmissions) {
        try {
          await this.prepareLastUserSubmission(submission.user);
          usernames.push(submission.user.username);
        } catch (err) {
          console.log(err);
        }
      }
      // create 100 boards
      // run 100 matches for every combination of 2 submissions
      for(let i = 0; i < 100; i++) {
        const initialMap = this.generateInitialMap();
        for (let catUser of usernames) {
          for (let catcherUser of usernames) {
            if (catUser == catcherUser) continue;
            await this.runMatch(catUser, catcherUser, initialMap);
          }
        }
      }
      // generate report

      competition.state = CompetitionRunState.FINISHED;
      competition = await this.runRepository.save(competition);
    } catch (err) {
      competition.state = CompetitionRunState.FAILED;
      competition = await this.runRepository.save(competition);
      throw err;
    }
  }
}
