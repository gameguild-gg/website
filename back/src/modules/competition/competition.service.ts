import { Injectable } from '@nestjs/common';
import { AuthService } from '../auth/auth.service';
import { TerminalDto } from './dtos/terminal.dto';
import * as util from 'util';
const exec = util.promisify(require('child_process').exec);
import * as fs from 'fs/promises';

@Injectable()
export class CompetitionService {
  constructor(public authService: AuthService) {}

  getDate(): string {
    return new Date().toISOString();
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
