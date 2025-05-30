const simpleGit = require('simple-git');
const fs = require('fs');
const path = require('path');
const { analyzeCommits } = require('@semantic-release/commit-analyzer');

async function getNextVersion() {
  try {
    const git = simpleGit();
    
    // Get all commits since last tag
    const tags = await git.tags();
    const latestTag = tags.latest || 'v0.0.0';
    const commits = await git.log(['--format=%B%n%n%H', `${latestTag}..HEAD`]);
    
    // Parse commits into conventional format
    const parsedCommits = commits.all.map(commit => ({
      hash: commit.hash,
      message: commit.message,
      subject: commit.message.split('\n')[0],
      body: commit.message.split('\n').slice(1).join('\n'),
      type: commit.message.split(':')[0].toLowerCase()
    }));

    // Analyze commits to determine next version
    const nextRelease = await analyzeCommits(
      { preset: 'angular' },
      { commits: parsedCommits, logger: console }
    );

    // If no version bump is needed, use the latest tag
    if (!nextRelease) {
      return latestTag;
    }

    return nextRelease;
  } catch (error) {
    console.error('Error getting next version:', error);
    return 'v0.0.1';
  }
}

async function generateGitStats() {
  console.log('Generating git stats...');
  const STATS_FILE_PATH = path.join(process.cwd(), 'git-stats.json');
  
  try {
    const git = simpleGit();

    // Get the log with stats
    let log = await git.raw(['log', '--numstat', '--format=%an']);

    // Filter out unwanted lines
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

    const newStats = [];

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
    console.error('Error generating git stats:', error);
    return [];
  }
}

async function getCommitHash() {
  try {
    const git = simpleGit();
    return await git.revparse(['--short', 'HEAD']);
  } catch (error) {
    console.error('Error getting commit hash:', error);
    return 'unknown';
  }
}

async function main() {
  try {
    const [stats, nextVersion, commit] = await Promise.all([
      generateGitStats(),
      getNextVersion(),
      getCommitHash()
    ]);
    
    const fullVersion = `${nextVersion}.${commit.trim()}`;
    
    const output = {
      version: fullVersion,
      stats,
      generatedAt: new Date().toISOString()
    };
    
    fs.writeFileSync('git-stats.json', JSON.stringify(output, null, 2));
    console.log('Successfully generated git-stats.json');
  } catch (error) {
    console.error('Failed to generate git stats:', error);
    process.exit(1);
  }
}

main(); 