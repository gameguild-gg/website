import ContributorCard, { Contributor } from '@/components/contributors/ContributorCard';
import { Github } from 'lucide-react';
import { Metadata } from 'next';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { exec } from 'child_process';
import { promisify } from 'util';
import fs from 'fs';
import path from 'path';
import simpleGit from 'simple-git';

const execAsync = promisify(exec);

// Git stats interface
interface GitStats {
  username: string;
  additions: number;
  deletions: number;
}

// Path to the stats file that might be generated at build time
const STATS_FILE_PATH = path.join(process.cwd(), 'git-stats.json');

/**
 * Generate git stats from the git repository
 */
async function generateGitStats(): Promise<GitStats[]> {
  try {
    // Check if we're in production and if the stats file exists
    if (process.env.NODE_ENV === 'production' && fs.existsSync(STATS_FILE_PATH)) {
      console.log('Using pre-generated git stats from file');
      const data = fs.readFileSync(STATS_FILE_PATH, 'utf8');
      return JSON.parse(data);
    }

    // In development mode or if stats file doesn't exist, generate stats
    console.log('Generating git stats...');

    // Use simpleGit for cross-platform compatibility
    const git = simpleGit();

    // Get the log with stats
    let log = await git.raw(['log', '--numstat', '--format=%an']);

    // Filter out unwanted lines in a cross-platform way
    log = log
      .split('\n')
      .filter(
        (line) =>
          !line.includes('package-lock.json') &&
          !line.includes('yarn.lock') &&
          !line.includes('node_modules/'),
      )
      .join('\n');

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
    return [];
  }
}

// Get contributors data from GitHub and generate git stats directly
async function getContributors(): Promise<Contributor[]> {
  // Fetch GitHub contributors
  const res = await fetch(
    'https://api.github.com/repos/gameguild-gg/website/contributors',
    { next: { revalidate: 3600 } },
  );

  if (!res.ok) throw new Error('Failed to fetch contributors');
  
  // Remove users semantic-release-bot and dependabot[bot] and LMD9977 from contributors
  const contributors = (await res.json()).filter(
    (contributor: Contributor) =>
      contributor.login !== 'semantic-release-bot' &&
      contributor.login !== 'LMD9977' &&
      contributor.login !== 'dependabot[bot]',
  ) as Contributor[];

  try {
    // Generate git stats directly instead of making an API call
    const stats = await generateGitStats();

    // Get additions and deletions from git stats and add them to the contributors
    for (const contributor of contributors) {
      const stat = stats.find(stat => stat.username === contributor.login);
      if (stat) {
        contributor.additions = stat.additions;
        contributor.deletions = stat.deletions;
      }
    }

    // Sort contributors by number of additions and deletions
    contributors.sort(
      (a, b) => b.additions + b.deletions - (a.additions + a.deletions),
    );
  } catch (error) {
    console.error('Error fetching git stats:', error);
    // Still return contributors even if stats can't be fetched
  }

  return contributors;
}

// type Props = {
//   params: Promise<{ id: string }>;
//   searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
// };

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: {
      template: '%s | Contributors',
      default: 'Contributors',
    },
    description: 'List of contributors to the Game Guild website',
  };
}

export default async function ContributorsPage() {
  const contributors = await getContributors();

  return (
    <div className="container mx-auto px-4 py-8">
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-1">
        <Github /> Github Contributors
      </h2>
      <div className="gap-6 justify-center flex">
        <Button>
          <Link
            href="https://github.com/gameguild-gg/website/"
            className="font-bold text-center flex items-center justify-center gap-1"
          >
            <Github /> Github Repo
          </Link>
        </Button>
        <Button>
          <Link href="https://github.com/gameguild-gg/website/stargazers">
            <img
              alt="GitHub Repo stars"
              src="https://img.shields.io/github/stars/gameguild-gg/website?style=social"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://github.com/gameguild-gg/website/commits/">
            <img
              alt="GitHub Last Commit"
              src="https://img.shields.io/github/last-commit/gameguild-gg/website"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://status.gameguild.gg/status/gg">
            <img
              alt="Uptime 30d"
              src="https://status.gameguild.gg/api/badge/1/uptime/720?label=Uptime%20(30d)"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://discord.gg/tac5fZ2bGh">
            <img
              alt="Discord chat"
              src="https://img.shields.io/discord/956922983727915078?logo=discord"
            />
          </Link>
        </Button>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {contributors.map((contributor) => (
          <ContributorCard key={contributor.login} {...contributor} />
        ))}
      </div>
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-6 mt-8">
        Repo Visualization
      </h2>
      <div className="flex flex-wrap justify-center gap-4">
        <Link href="https://github.com/gameguild-gg/website/stargazers">
          <img
            src="https://api.star-history.com/svg?repos=gameguild-gg/website&type=Date"
            style={{
              width: '30%',
              height: 'auto',
              minWidth: '600px',
              minHeight: '400px',
            }}
            className="w-full max-w-lg mx-auto"
            alt="star history"
          />
        </Link>
        <video
          className="w-full max-w-lg mx-auto"
          src="https://gameguild-gg.github.io/website/gource.mp4"
          controls
          loop
          autoPlay
          muted
        >
          Your browser does not support the video tag.
        </video>
      </div>
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-6 mt-8">
        OpenCollective Contributors
      </h2>
      <p>
        While we are applying to OpenCollective, we are not able to accept any
        donations. But you can still support us by contributing to our GitHub
        repository, and being part of our community on whatsapp and discord.
      </p>
    </div>
  );
}
