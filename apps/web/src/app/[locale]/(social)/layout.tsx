import { auth } from '@/auth';
import { SocialAppShell } from '@/components/feed/social-app-shell';
import { AccessibilitySyncInitializer } from '@/components/settings/accessibility-sync-initializer';
import { EditorPreferencesSyncInitializer } from '@/components/settings/editor-preferences-sync-initializer';
import { ThemeSyncInitializer } from '@/components/settings/theme-sync-initializer';
import { redirect } from '@/i18n/navigation';
import React from 'react';

export default async function Layout({ children, params }: LayoutProps<'/[locale]'>): Promise<React.JSX.Element> {
  const { locale } = await params;
  const session = await auth();

  if (!session || typeof session === 'function') {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/social' } }, locale });
    throw new Error('Unauthenticated social access');
  }

  return (
    <SocialAppShell>
      <ThemeSyncInitializer />
      <AccessibilitySyncInitializer />
      <EditorPreferencesSyncInitializer />
      {children}
    </SocialAppShell>
  );
}
