import { NextResponse } from 'next/server';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

let cachedVersion: string | null = null;

async function getVersion() {
  if (cachedVersion) {
    return cachedVersion;
  }

  try {
    const { stdout: tag } = await execAsync('git describe --abbrev=0 --tags');
    const { stdout: commit } = await execAsync('git rev-parse --short HEAD');
    cachedVersion = `${tag.trim()}.${commit.trim()}`;
    return cachedVersion;
  } catch (error) {
    console.error('Error fetching version:', error);
    return 'v0.0.1';
  }
}

export async function GET() {
  const version = await getVersion();
  return NextResponse.json({ version });
}
