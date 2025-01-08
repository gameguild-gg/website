import { NextRequest, NextResponse } from 'next/server';
import { Octokit } from 'octokit';
import NodeCache from 'node-cache';

const octokit = new Octokit({ auth: process.env.GITHUB_TOKEN }).rest;
const cache = new NodeCache({ stdTTL: 3600 }); // 1 hour TTL

async function fetchAllIssues(state: 'open' | 'closed' | 'all') {
  const cacheKey = `all_issues_${state}`;
  const cachedIssues = cache.get(cacheKey);

  if (cachedIssues) {
    return cachedIssues;
  }

  let allIssues = [];
  let page = 1;
  let hasNextPage = true;

  while (hasNextPage) {
    const response = await octokit.issues.listForRepo({
      owner: 'gameguild-gg',
      repo: 'website',
      state: state,
      per_page: 100,
      page: page,
    });

    allIssues = [...allIssues, ...response.data];
    hasNextPage = response.data.length === 100;
    page++;
  }

  cache.set(cacheKey, allIssues);
  return allIssues;
}

export async function GET(request: NextRequest) {
  try {
    const url = new URL(request.url);
    const issueType = url.searchParams.get('issueType') || 'all';
    const assignee = url.searchParams.get('assignee') || 'all';
    const labels =
      url.searchParams.get('labels')?.split(',').filter(Boolean) || [];
    const sort = url.searchParams.get('sort') || 'newest';
    const page = parseInt(url.searchParams.get('page') || '1', 10);
    const pageSize = parseInt(url.searchParams.get('pageSize') || '25', 10);
    const state = url.searchParams.get('state') || 'open';

    const allIssues = await fetchAllIssues(state as 'open' | 'closed' | 'all');

    if (!Array.isArray(allIssues)) {
      console.error('fetchAllIssues did not return an array:', allIssues);
      return NextResponse.json(
        { error: 'Invalid data format' },
        { status: 500 },
      );
    }

    let filteredIssues = allIssues;

    // Apply filters
    if (issueType === 'issue') {
      filteredIssues = filteredIssues.filter((issue) => !issue.pull_request);
    } else if (issueType === 'pr') {
      filteredIssues = filteredIssues.filter((issue) => issue.pull_request);
    }

    if (assignee && assignee !== 'all') {
      filteredIssues = filteredIssues.filter(
        (issue) =>
          issue.assignees && issue.assignees.some((a) => a.login === assignee),
      );
    }

    if (labels.length > 0) {
      filteredIssues = filteredIssues.filter((issue) =>
        labels.every(
          (label) => issue.labels && issue.labels.some((l) => l.name === label),
        ),
      );
    }

    // Apply sorting
    filteredIssues.sort((a, b) => {
      switch (sort) {
        case 'oldest':
          return (
            new Date(a.created_at).getTime() - new Date(b.created_at).getTime()
          );
        case 'newest':
          return (
            new Date(b.created_at).getTime() - new Date(a.created_at).getTime()
          );
        case 'most-reactions':
          return (
            (b.reactions?.total_count || 0) - (a.reactions?.total_count || 0)
          );
        case 'least-reactions':
          return (
            (a.reactions?.total_count || 0) - (b.reactions?.total_count || 0)
          );
        default:
          return 0;
      }
    });

    // Apply pagination
    const totalIssues = filteredIssues.length;
    const totalPages = Math.ceil(totalIssues / pageSize);
    const paginatedIssues = filteredIssues.slice(
      (page - 1) * pageSize,
      page * pageSize,
    );

    return NextResponse.json({
      issues: paginatedIssues,
      totalIssues,
      totalPages,
      currentPage: page,
    });
  } catch (error) {
    console.error('Error fetching issues:', error);
    return NextResponse.json(
      { error: 'Failed to fetch issues' },
      { status: 500 },
    );
  }
}
