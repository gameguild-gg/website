import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMemberProjectMock } = vi.hoisted(() => ({
  getMemberProjectMock: vi.fn(),
}));

vi.mock('@/lib/community/queries/members', () => ({
  getPublicMemberProfile: vi.fn(),
  getMemberProject: getMemberProjectMock,
}));

import { generateMetadata } from './page';

describe('member portfolio project page metadata', () => {
  beforeEach(() => {
    getMemberProjectMock.mockReset();
  });

  it('derives title and description from the portfolio project', async () => {
    getMemberProjectMock.mockResolvedValue({
      member: { displayName: 'Ada Lovelace' },
      project: {
        title: 'Analytical Engine',
        description: 'A general-purpose computing machine.',
        tech: 'Brass and steam',
        imageUrl: 'https://example.test/engine.png',
      },
    });

    const metadata = await generateMetadata({
      params: Promise.resolve({ member: 'ada', project: 'analytical-engine' }),
    });

    expect(getMemberProjectMock).toHaveBeenCalledWith('ada', 'analytical-engine');
    expect(metadata).toMatchObject({
      title: 'Analytical Engine by Ada Lovelace',
      description: 'A general-purpose computing machine.',
      openGraph: {
        title: 'Analytical Engine by Ada Lovelace',
        description: 'A general-purpose computing machine.',
        images: ['https://example.test/engine.png'],
      },
    });
  });

  it('falls back to noindex metadata when the project is missing', async () => {
    getMemberProjectMock.mockResolvedValue(null);

    const metadata = await generateMetadata({
      params: Promise.resolve({ member: 'ada', project: 'missing' }),
    });

    expect(metadata).toMatchObject({
      title: 'Project Not Found',
      robots: { index: false, follow: false },
    });
  });
});
