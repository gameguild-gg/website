import { ProfileHeader, ProfileTabs } from '@game-guild/community-members';
import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import React from 'react';
import { getPublicMemberProfile } from '@/lib/community/queries/members';

// Dynamic metadata for the public member profile; shares the request-scoped profile
// lookup with the page body.
export async function generateMetadata({ params }: { params: Promise<{ member: string }> }): Promise<Metadata> {
  const { member } = await params;
  const profile = await getPublicMemberProfile(member);

  if (!profile) {
    return {
      title: 'Member Not Found',
      description: 'The requested member profile could not be found.',
      robots: { index: false, follow: false },
    };
  }

  const title = `${profile.displayName} (@${profile.username})`;
  const description =
    profile.headline?.trim() ||
    profile.bio?.trim().slice(0, 200) ||
    `${profile.displayName} on GameGuild.`;

  return {
    title,
    description,
    openGraph: {
      title,
      description,
      ...(profile.avatarUrl ? { images: [profile.avatarUrl] } : {}),
    },
  };
}

export default async function Page({
  params,
}: PageProps<'/[locale]/members/[member]'>): Promise<React.JSX.Element> {
  const { member } = await params;
  const profile = await getPublicMemberProfile(member);

  if (!profile) notFound();

  return (
    <div className="flex min-h-screen flex-col">
      <ProfileHeader
        username={profile.username}
        displayName={profile.displayName}
        initials={profile.initials}
        joinDate={profile.joinDate}
        headline={profile.headline}
        location={profile.location}
        avatarUrl={profile.avatarUrl}
        bannerUrl={profile.bannerUrl}
        stats={profile.stats}
      />
      <div className="mx-auto w-full max-w-7xl px-6 py-8">
        <ProfileTabs
          username={profile.username}
          displayName={profile.displayName}
          initials={profile.initials}
          joinDate={profile.joinDate}
          bio={profile.bio}
          featuredProject={profile.featuredProject}
          portfolioProjects={profile.portfolioProjects}
          technicalSkills={profile.technicalSkills}
          toolsSkills={profile.toolsSkills}
          activities={profile.activities}
        />
      </div>
    </div>
  );
}
