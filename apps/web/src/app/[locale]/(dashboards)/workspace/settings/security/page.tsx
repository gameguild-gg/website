import { SessionsManager } from '@/components/settings/sessions-manager';
import { loadSessionsData } from '@/lib/auth/sessions-actions';
import { getTranslations } from 'next-intl/server';
import React from 'react';

/**
 * Settings → Security: session management (issue #278).
 *
 * Server-rendered session, security-analysis, and trusted-device data; the client
 * manager performs renames, terminations, and trust mutations through server actions.
 */
export default async function SecuritySettingsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<React.JSX.Element> {
  const { locale } = await params;
  const [t, data] = await Promise.all([getTranslations({ locale, namespace: 'settings.security' }), loadSessionsData()]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground">{t('description')}</p>
      </header>
      <SessionsManager data={data} locale={locale} />
    </div>
  );
}
