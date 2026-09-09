import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingLabDashboard: vi.fn(),
  getTestingProjectOptions: vi.fn(),
}));

vi.mock('@/lib/testing-lab', () => ({
  getTestingLabDashboard: mocks.getTestingLabDashboard,
  getTestingProjectOptions: mocks.getTestingProjectOptions,
  normalizeTestingRequestStatus: (status?: string) => status ?? 'Draft',
}));

vi.mock('@/components/testing-lab/testing-lab-dialogs', () => ({
  SubmitTestingBuildDialog: () => <button type="button">Submit project</button>,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href }: { children: ReactNode; href: string }) => <a href={href}>{children}</a>,
}));

import TestingLabProjectsPage from './page';

describe('Testing Lab projects page', () => {
  it('gives the project search and status filter explicit accessible names', async () => {
    mocks.getTestingLabDashboard.mockResolvedValue({ requests: [], accessIssues: [] });
    mocks.getTestingProjectOptions.mockResolvedValue([]);

    render(await TestingLabProjectsPage({ searchParams: Promise.resolve({}) }));

    expect(screen.getByRole('textbox', { name: 'Search community projects' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Filter projects by status' })).toBeInTheDocument();
  });

  it('lists archived requests so operators can restore them', async () => {
    mocks.getTestingLabDashboard.mockResolvedValue({
      requests: [{ id: 'archived-request', title: 'Archived build', status: 'Draft', isDeleted: true }],
      accessIssues: [],
    });
    mocks.getTestingProjectOptions.mockResolvedValue([]);

    render(await TestingLabProjectsPage({ searchParams: Promise.resolve({ status: 'Archived' }) }));

    expect(screen.getByRole('option', { name: 'Archived' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Archived build' })).toHaveAttribute(
      'href',
      '/workspace/testing-lab/projects/archived-request',
    );
    expect(screen.getAllByText('Archived')).toHaveLength(2);
  });
});
