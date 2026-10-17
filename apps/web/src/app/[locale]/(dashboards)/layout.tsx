import { auth } from '@/auth';
import { GitHubStarPrompt } from '@/components/community/github-star-prompt';
import { WorkspaceShell } from '@/components/console/workspace-shell';
import { AccessibilitySyncInitializer } from '@/components/settings/accessibility-sync-initializer';
import { EditorPreferencesSyncInitializer } from '@/components/settings/editor-preferences-sync-initializer';
import { ThemeSyncInitializer } from '@/components/settings/theme-sync-initializer';
import { TwoFactorNudgeBanner } from '@/components/settings/two-factor-nudge-banner';
import { redirect } from '@/i18n/navigation';
import { getDashboardNotificationSummary } from '@/lib/dashboard-notifications';
import { getDashboardContexts } from '@/lib/dashboard-contexts';
import { getMfaStatus } from '@/lib/user-settings/mfa-actions';
import React from 'react';

/**
 * Defensive MFA status read for the no-2FA nudge banner (issue #838): any
 * failure means "don't render the banner" — the nudge must fail silent.
 */
async function fetchTwoFactorEnabled(): Promise<boolean | null> {
  try {
    const status = await getMfaStatus();
    if (!status.ok) return null;
    return status.data.isEnabled ?? false;
  } catch {
    return null;
  }
}

export default async function Layout({ children, params }: LayoutProps<'/[locale]'>): Promise<React.JSX.Element> {
  const { locale } = await params;

  const session = await auth();

  if (!session || typeof session === 'function') {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/workspace' } }, locale });
    throw new Error('Unauthenticated dashboard access');
  }

  const [notifications, dashboardContexts, twoFactorEnabled] = await Promise.all([
    getDashboardNotificationSummary(session.user.id),
    getDashboardContexts(),
    fetchTwoFactorEnabled(),
  ]);
  const dashboardUser = {
    id: session.user.id,
    name: session.user.name?.trim() || session.user.email?.split('@')[0] || 'GameGuild user',
    email: session.user.email ?? 'No email available',
    image: session.user.image ?? null,
  };

  return (
    <WorkspaceShell
      notifications={notifications}
      user={dashboardUser}
      capabilities={dashboardContexts.capabilities}
      contexts={dashboardContexts.contexts}
    >
      <ThemeSyncInitializer />
      <AccessibilitySyncInitializer />
      <EditorPreferencesSyncInitializer />
      {twoFactorEnabled === false && <TwoFactorNudgeBanner twoFactorEnabled={false} />}
      <GitHubStarPrompt />
      {children}
    </WorkspaceShell>
  );
}
