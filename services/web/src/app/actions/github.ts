'use server';

import { Octokit } from '@octokit/rest';

const octokit = new Octokit({
  auth: process.env.GITHUB_TOKEN,
});

export async function fetchGitHubIssues() {
  try {
    const response = await octokit.issues.listForRepo({
      owner: 'gameguild-gg',
      repo: 'website',
      state: 'all',
      per_page: 100,
    });
    return response.data;
  } catch (error) {
    console.error('Error fetching issues:', error);
    throw new Error('Failed to fetch issues');
  }
}
