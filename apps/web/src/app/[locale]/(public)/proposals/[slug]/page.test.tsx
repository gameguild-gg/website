import '@testing-library/jest-dom/vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getPublishedProposal: vi.fn(),
  submitProposalInterest: vi.fn(),
  submitCourseProposal: vi.fn(),
}));

vi.mock('@/lib/proposals/actions', () => ({
  getPublishedProposal: mocks.getPublishedProposal,
  submitProposalInterestAction: mocks.submitProposalInterest,
  submitCourseProposalAction: mocks.submitCourseProposal,
}));

vi.mock('next/navigation', () => ({
  notFound: vi.fn(() => {
    throw new Error('not-found');
  }),
  usePathname: vi.fn(() => '/proposals/shader-basics-ab12cd34'),
}));

import ProposalDetailPage from './page';

const publishedProposal = {
  id: '0193aaac-0000-7000-8000-000000000001',
  slug: 'shader-basics-ab12cd34',
  status: 'published',
  courseTitle: 'Shader Basics',
  pitch: 'Hands-on shader authoring.',
  audience: 'Intermediate developers.',
  proposerName: 'Ada',
  metadata: null,
  publishedAt: '2026-10-01T00:00:00.000Z',
};

const props = { params: Promise.resolve({ slug: 'shader-basics-ab12cd34' }) } as never;

describe('/proposals/[slug] detail page (issue #105)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.submitProposalInterest.mockResolvedValue({
      success: true,
      message: 'Thanks! Your interest has been recorded.',
    });
  });

  afterEach(cleanup);

  it('renders a published proposal with pitch, audience, and proposer', async () => {
    mocks.getPublishedProposal.mockResolvedValue(publishedProposal);

    render(await ProposalDetailPage(props));

    expect(screen.getByRole('heading', { name: 'Shader Basics' })).toBeInTheDocument();
    expect(screen.getByText('Hands-on shader authoring.')).toBeInTheDocument();
    expect(screen.getByText('Intermediate developers.')).toBeInTheDocument();
    expect(screen.getByText(/Proposed by Ada/)).toBeInTheDocument();
    expect(mocks.getPublishedProposal).toHaveBeenCalledWith('shader-basics-ab12cd34');
  });

  it('404s for missing or unpublished proposals', async () => {
    mocks.getPublishedProposal.mockResolvedValue(null);

    await expect(ProposalDetailPage(props)).rejects.toThrow('not-found');
  });

  it('registers interest through the course-proposal lead pipeline', async () => {
    mocks.getPublishedProposal.mockResolvedValue(publishedProposal);

    render(await ProposalDetailPage(props));

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Grace' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'grace@example.com' } });
    fireEvent.change(screen.getByLabelText('What would you want to learn?'), {
      target: { value: 'More node-based shaders please.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Register interest' }));

    await waitFor(() =>
      expect(screen.getByText('Thanks! Your interest has been recorded.')).toBeInTheDocument(),
    );
    expect(mocks.submitProposalInterest).toHaveBeenCalledWith({
      name: 'Grace',
      email: 'grace@example.com',
      message: 'More node-based shaders please.',
      pagePath: '/proposals/shader-basics-ab12cd34',
    });
  });

  it('surfaces validation errors from the action', async () => {
    mocks.getPublishedProposal.mockResolvedValue(publishedProposal);
    mocks.submitProposalInterest.mockResolvedValue({
      success: false,
      error: 'Message must be at least 10 characters.',
    });

    render(await ProposalDetailPage(props));

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Grace' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'grace@example.com' } });
    fireEvent.change(screen.getByLabelText('What would you want to learn?'), {
      target: { value: 'short' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Register interest' }));

    await waitFor(() =>
      expect(screen.getByText('Message must be at least 10 characters.')).toBeInTheDocument(),
    );
  });
});
