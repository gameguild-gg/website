import { NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

export async function GET() {
  try {
    let version = 'v0.0.1';

    // First attempt: try reading file from filesystem (most reliable in Next.js API routes)
    try {
      const gitStatsPath = path.resolve(process.cwd(), 'git-stats.json');

      if (fs.existsSync(gitStatsPath)) {
        const gitStatsContent = fs.readFileSync(gitStatsPath, 'utf-8');
        const gitStats = JSON.parse(gitStatsContent);
        version = gitStats.version;
      }
    } catch (fsError) {
      console.error('Error reading git-stats.json from filesystem:', fsError);
    }

    return NextResponse.json({ version });
  } catch (error) {
    console.error('Error in version API:', error);
    return NextResponse.json({ version: 'v0.0.1', error: 'Failed to fetch version' }, { status: 500 });
  }
}
