import { auth } from '@/auth';
import { ProfileForm } from '@/components/settings/profile-form';
import { getCommunityProfile, getProfile } from '@/lib/user-settings/queries';
import { getTranslations } from 'next-intl/server';
import React from 'react';

export default async function ProfileSettingsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<React.JSX.Element> {
  const { locale } = await params;
  const [t, session, profile, socialProfile] = await Promise.all([
    getTranslations({ locale, namespace: 'settings.profile' }),
    auth(),
    getProfile(),
    getCommunityProfile().catch(() => null),
  ]);
  const accountName = session && typeof session !== 'function'
    ? session.user.name?.trim() || session.user.email?.split('@')[0] || t('fallbackName')
    : t('fallbackName');
  const accountEmail = session && typeof session !== 'function'
    ? session.user.email ?? t('fallbackEmail')
    : t('fallbackEmail');
  const currentUserId = session && typeof session !== 'function' ? session.user.id : null;
  const fallbackHandle = `${accountEmail.split('@')[0] ?? 'creator'}-${currentUserId?.slice(0, 6) ?? 'profile'}`
    .toLowerCase()
    .replace(/[^a-z0-9_-]/g, '-');

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground">{t('description')}</p>
      </header>
      <ProfileForm
        accountName={accountName}
        accountEmail={accountEmail}
        defaultValues={{
          handle: socialProfile?.handle ?? fallbackHandle,
          displayName: profile?.displayName ?? socialProfile?.displayName ?? accountName,
          bio: profile?.bio ?? '',
          location: profile?.location ?? '',
          website: profile?.website ?? '',
          jobTitle: profile?.jobTitle ?? '',
          company: profile?.company ?? '',
          avatarUrl: socialProfile?.avatarUrl ?? '',
          bannerUrl: socialProfile?.bannerUrl ?? '',
        }}
        defaultVisibility={socialProfile ? socialProfile.visibility : null}
        hasCommunityProfile={socialProfile !== null}
        publicProfilePath={`/social/profiles/${socialProfile?.handle ?? fallbackHandle}`}
      />
    </div>
  );
}
