import { ConflictException, Injectable } from "@nestjs/common";
import { AuthService } from "../auth/auth.service";
import { TerminalDto } from "./dtos/terminal.dto";
import * as util from "util";
import { UserEntity } from "../user/user.entity";
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { CompetitionSubmissionEntity } from "./entities/competition.submission.entity";
import { CompetitionRunEntity, CompetitionRunState } from "./entities/competition.run.entity";
import { CompetitionMatchEntity } from "./entities/competition.match.entity";
import extract from "extract-zip";
import fs from "fs/promises";
import process from "process";
import * as fse from "fs-extra";
import { UserService } from "../user/user.service";

const exec = util.promisify(require('child_process').exec);

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
      // create 100 boards
      // run 100 matches for every combination of 2 submissions
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
