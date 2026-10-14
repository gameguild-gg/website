import { SecurityForm } from '@/components/settings/security-form';
import { getMfaStatus } from '@/lib/user-settings/mfa-actions';
import { getTranslations } from 'next-intl/server';
import React from 'react';

export default async function SecuritySettingsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<React.JSX.Element> {
  const { locale } = await params;
  const [t, status] = await Promise.all([
    getTranslations({ locale, namespace: 'settings.security' }),
    getMfaStatus(),
  ]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground">{t('description')}</p>
      </header>
      <SecurityForm
        initialEnabled={status.ok ? (status.data.isEnabled ?? false) : false}
        initialBackupCodesRemaining={status.ok ? status.data.backupCodesRemaining ?? null : null}
      />
    </div>
  );
}
