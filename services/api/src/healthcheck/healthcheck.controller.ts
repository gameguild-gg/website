import { Controller, Get, InternalServerErrorException } from '@nestjs/common';
import { simpleGit } from 'simple-git';
import { ApiOkResponse, ApiProperty, ApiResponse } from '@nestjs/swagger';

export class GitStats {
  @ApiProperty()
  username: string;
  @ApiProperty()
  additions: number;
  @ApiProperty()
  deletions: number;
}

@Controller('healthcheck')
export class HealthcheckController {
  constructor() {}

  @Get('gitstats')
  @ApiOkResponse({ type: GitStats, isArray: true })
  async gitstats(): Promise<GitStats[]> {
    try {
      const git = simpleGit();

      // Get the log with stats
      const log = await git.raw(['log', '--numstat', '--format=%an']);

      // Parse the log to calculate stats
      const stats = {};
      const lines = log.split('\n');

      let currentAuthor = '';
      for (const line of lines) {
        if (!line.trim()) continue;

        // If the line is an author
        if (!line.includes('\t')) {
          currentAuthor = line.trim();
          if (!stats[currentAuthor]) {
            stats[currentAuthor] = { additions: 0, deletions: 0 };
          }
        } else {
          // If the line contains stats
          const [additions, deletions] = line
            .split('\t')
            .map((x) => parseInt(x, 10) || 0);
          stats[currentAuthor].additions += additions;
          stats[currentAuthor].deletions += deletions;
        }
      }

      // remove semantic-release-bot and dependabot[bot] from stats
      delete stats['semantic-release-bot'];
      delete stats['dependabot[bot]'];

      // accumulate stats from one author to another and remove the previous author
      // map of from -> to
      const authorMap = {
        'Alexandre Tolstenko Nogueira': 'Alexandre Tolstenko',
        LMD9977: 'Nominal9977',
      };

      for (const [from, to] of Object.entries(authorMap)) {
        if (stats[from] && stats[to]) {
          stats[to].additions += stats[from].additions;
          stats[to].deletions += stats[from].deletions;
          delete stats[from];
        }
      }

      // rename name to username
      // map of name -> github username
      const usernameMap = {
        'Alexandre Tolstenko': 'tolstenko',
        'Alec Santos': 'alec-o-mago',
        'Miguel Moroni': 'migmoroni',
        'Matheus Martins': 'mathrmartins',
        Nominal9977: 'Nominal9977',
        hdorer: 'hdorer',
        'Joel Oliveira': 'vikumm',
        Germano: 'Germano123',
      };

      const newStats: GitStats[] = [];

      for (const [name, username] of Object.entries(usernameMap)) {
        if (stats[name]) {
          newStats.push({ ...stats[name], username: username });
        }
      }
      // sort by sum of additions and deletions
      newStats.sort(
        (a, b) => b.additions + b.deletions - (a.additions + a.deletions),
      );

      return newStats;
    } catch (error) {
      console.error('Error fetching Git stats:', error);
      throw new InternalServerErrorException('Failed to fetch Git stats');
    }
  }
}
