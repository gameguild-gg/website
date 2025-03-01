import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const q = searchParams.get('q');
  const author = searchParams.get('author');
  const assignee = searchParams.get('assignee');
  const label = searchParams.get('label');
  const sort = searchParams.get('sort');

  try {
    const response = await fetch(
      `https://api.github.com/repos/gameguild-gg/website/issues?${searchParams.toString()}`,
      {
        headers: {
          Accept: 'application/vnd.github.v3+json',
          Authorization: `Bearer ${process.env.GITHUB_TOKEN}`,
        },
      },
    );

    if (!response.ok) {
      throw new Error('Failed to fetch issues');
    }

    const issues = await response.json();
    return NextResponse.json(issues);
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to fetch issues' },
      { status: 500 },
    );
  }
}
