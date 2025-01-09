export const dynamic = 'force-dynamic';

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
    const [issuesResponse, pullsResponse] = await Promise.all([
      octokit.issues.listForRepo({
        owner: 'gameguild-gg',
        repo: 'website',
        state: state,
        per_page: 100,
        page: page,
      }),
      octokit.pulls.list({
        owner: 'gameguild-gg',
        repo: 'website',
        state: state,
        per_page: 100,
        page: page,
      }),
    ]);

    const issuesWithReviews = await Promise.all(
      issuesResponse.data.map(async (issue) => {
        if (issue.pull_request) {
          // Get both requested reviewers and actual reviews
          const [requestedReviewers, reviews] = await Promise.all([
            octokit.pulls.listRequestedReviewers({
              owner: 'gameguild-gg',
              repo: 'website',
              pull_number: issue.number,
            }),
            octokit.pulls.listReviews({
              owner: 'gameguild-gg',
              repo: 'website',
              pull_number: issue.number,
            }),
          ]);

          // Get unique reviewers from both requested and completed reviews
          const allReviewers = [
            ...requestedReviewers.data.users,
            ...reviews.data.map((review) => review.user),
          ].filter(
            (reviewer, index, self) =>
              reviewer && // ensure reviewer exists
              index === self.findIndex((r) => r && r.login === reviewer.login), // deduplicate
          );

          return {
            ...issue,
            reviewers: allReviewers,
            requested_reviewers: requestedReviewers.data.users,
          };
        }
        return issue;
      }),
    );

    allIssues = [...allIssues, ...issuesWithReviews];
    hasNextPage = issuesResponse.data.length === 100;
    page++;
  }

  cache.set(cacheKey, allIssues);
  return allIssues;
}

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const issueType = searchParams.get('issueType') || 'all';
    const user = searchParams.get('user') || 'all';
    const labels = searchParams.get('labels')?.split(',').filter(Boolean) || [];
    const sort = searchParams.get('sort') || 'newest';
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '25', 10);
    const state = searchParams.get('state') || 'open';

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

    if (user && user !== 'all') {
      filteredIssues = filteredIssues.filter(
        (issue) =>
          (issue.user && issue.user.login === user) ||
          (issue.assignees && issue.assignees.some((a) => a.login === user)) ||
          (issue.reviewers && issue.reviewers.some((r) => r.login === user)) ||
          (issue.requested_reviewers &&
            issue.requested_reviewers.some((r) => r.login === user)),
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
