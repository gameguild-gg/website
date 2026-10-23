import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getPublicMemberProfileMock } = vi.hoisted(() => ({
  getPublicMemberProfileMock: vi.fn(),
}));

vi.mock('@/lib/community/queries/members', () => ({
  getPublicMemberProfile: getPublicMemberProfileMock,
  getMemberProject: vi.fn(),
}));
vi.mock('@game-guild/community-members', () => ({
  ProfileHeader: () => null,
  ProfileTabs: () => null,
}));

import { generateMetadata } from './page';

describe('member profile page metadata', () => {
  beforeEach(() => {
    getPublicMemberProfileMock.mockReset();
  });

  it('derives title and description from the public profile', async () => {
    getPublicMemberProfileMock.mockResolvedValue({
      username: 'ada',
      displayName: 'Ada Lovelace',
      headline: 'Engineer of analytical engines',
      avatarUrl: 'https://example.test/ada.png',
    });

    const metadata = await generateMetadata({ params: Promise.resolve({ member: 'ada' }) });

    expect(getPublicMemberProfileMock).toHaveBeenCalledWith('ada');
    expect(metadata).toMatchObject({
      title: 'Ada Lovelace (@ada)',
      description: 'Engineer of analytical engines',
      openGraph: {
        title: 'Ada Lovelace (@ada)',
        description: 'Engineer of analytical engines',
        images: ['https://example.test/ada.png'],
      },
    });
  });

  it('falls back to noindex metadata when the profile is missing', async () => {
    getPublicMemberProfileMock.mockResolvedValue(null);

    const metadata = await generateMetadata({ params: Promise.resolve({ member: 'nobody' }) });

    expect(metadata).toMatchObject({
      title: 'Member Not Found',
      robots: { index: false, follow: false },
    });
  });
});
