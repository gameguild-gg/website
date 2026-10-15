import { MfaMethodsManager } from '@/components/settings/mfa-methods-manager';
import { SecurityPoliciesCard } from '@/components/settings/security-policies-card';
import { SessionsManager } from '@/components/settings/sessions-manager';
import { loadMfaData } from '@/lib/auth/mfa-actions';
import { loadSessionsData } from '@/lib/auth/sessions-actions';
import { getTranslations } from 'next-intl/server';
import React from 'react';

/**
 * Settings → Security: policy summary (issue #838, display-only), session
 * management (issue #278) and MFA method management (issue #244).
 *
 * Server-rendered policy, session, security-analysis, trusted-device, MFA-configuration,
 * method and recovery-code data; the client managers perform mutations through server actions.
 */
export default async function SecuritySettingsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<React.JSX.Element> {
  const { locale } = await params;
  const [t, sessions, mfa] = await Promise.all([
    getTranslations({ locale, namespace: 'settings.security' }),
    loadSessionsData(),
    loadMfaData(),
  ]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground">{t('description')}</p>
      </header>
      <SecurityPoliciesCard locale={locale} />
      <MfaMethodsManager data={mfa} />
      <SessionsManager data={sessions} locale={locale} />
    </div>
  );
}
