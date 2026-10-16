import { getSecurityPolicies } from '@/lib/security/policies';
import { Badge } from '@game-guild/ui/components/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@game-guild/ui/components/card';
import { KeyRound, ShieldCheck, Timer } from 'lucide-react';
import React from 'react';
import { getTranslations } from 'next-intl/server';

/**
 * Security policy summary for the settings → Security section (issue #838, display-only v1).
 *
 * Values come from server-side environment configuration (`getSecurityPolicies`);
 * this surface renders them read-only. Editing the knobs is an administrator
 * concern on the deployment side.
 */
export async function SecurityPoliciesCard({ locale }: { locale: string }): Promise<React.JSX.Element> {
  const t = await getTranslations({ locale, namespace: 'settings.security.policies' });
  const policies = getSecurityPolicies();

  const rows = [
    {
      icon: ShieldCheck,
      title: t('twoFactor.title'),
      value: policies.twoFactorEnforcement === 'required' ? t('twoFactor.required') : t('twoFactor.optional'),
      description: t('twoFactor.description'),
    },
    {
      icon: Timer,
      title: t('sessionTimeout.title'),
      value: t('sessionTimeout.value', { minutes: policies.sessionTimeoutMinutes }),
      description: t('sessionTimeout.description'),
    },
    {
      icon: KeyRound,
      title: t('apiKeyRotation.title'),
      value: t('apiKeyRotation.value', { days: policies.apiKeyRotationDays }),
      description: t('apiKeyRotation.description'),
    },
  ];

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-xl">{t('title')}</CardTitle>
          <Badge variant="outline">{t('managedBadge')}</Badge>
        </div>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-3">
        {rows.map((row) => {
          const Icon = row.icon;
          return (
            <div key={row.title} className="rounded-xl border border-border bg-accent/30 p-4">
              <div className="flex items-center gap-2 text-primary">
                <Icon className="size-4" aria-hidden="true" />
                <p className="text-sm font-medium">{row.title}</p>
              </div>
              <p className="mt-2 text-lg font-semibold">{row.value}</p>
              <p className="mt-1 text-xs leading-5 text-muted-foreground">{row.description}</p>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
