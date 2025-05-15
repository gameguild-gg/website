import { NextResponse } from 'next/server';
import simpleGit from 'simple-git';
import fs from 'fs';
import path from 'path';

let cachedVersion: string | null = null;
const VERSION_FILE_PATH = path.join(process.cwd(), 'version.json');

async function getVersion() {
  if (cachedVersion) {
    return cachedVersion;
  }

  // Check if we're in production and a version file exists (generated at build time)
  if (process.env.NODE_ENV === 'production' && fs.existsSync(VERSION_FILE_PATH)) {
    try {
      console.log('Using pre-generated version from file');
      const data = fs.readFileSync(VERSION_FILE_PATH, 'utf8');
      const versionData = JSON.parse(data);
      cachedVersion = versionData.version;
      return cachedVersion;
    } catch (error) {
      console.error('Error reading version file:', error);
      // Continue to generate version dynamically
    }
  }

  // Generate version dynamically (development or fallback)
  try {
    const git = simpleGit();
    
    // Get the latest tag and commit
    const tags = await git.tags();
    const latestTag = tags.latest || 'v0.0.1';
    const commit = await git.revparse(['--short', 'HEAD']);
    
    cachedVersion = `${latestTag}.${commit.trim()}`;
    return cachedVersion;
  } catch (error) {
    console.error('Error fetching version:', error);
    return 'v0.0.1';
  }
}

export async function GET() {
  try {
    const version = await getVersion();
    return NextResponse.json({ version });
  } catch (error) {
    console.error('Error in version API:', error);
    return NextResponse.json(
      { version: 'v0.0.1', error: 'Failed to fetch version' },
      { status: 500 },
    );
  }
}
