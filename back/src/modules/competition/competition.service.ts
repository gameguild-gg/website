import { Injectable } from '@nestjs/common';
import { AuthService } from '../auth/auth.service';
import { TerminalDto } from './dtos/terminal.dto';
import * as util from 'util';
const exec = util.promisify(require('child_process').exec);
import * as fs from 'fs/promises';
import { UserEntity } from '../user/user.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CompetitionSubmissionEntity } from './entities/competition.submission.entity';
import { CompetitionRunEntity } from './entities/competition.run.entity';
import { CompetitionMatchEntity } from './entities/competition.match.entity';

@Injectable()
export class CompetitionService {
  constructor(
    public authService: AuthService,
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
}
