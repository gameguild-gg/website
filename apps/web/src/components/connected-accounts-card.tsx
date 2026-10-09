'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { GoogleLinkButton } from '@/components/google-link-button';
import { Button, buttonVariants } from '@game-guild/ui/components/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Callout } from '@/components/ui/callout';
import { revokeProviderScopes, unlinkProvider } from '@/lib/auth/external-logins-actions';
import { cn } from '@/lib/utils';
import { CheckCircle2, Loader2, ShieldCheck, Unlink, X } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';

export interface LinkedAccount {
  provider: string;
  linkedAt: string;
  /** OAuth scope tokens currently granted on this link (issue #250). */
  grantedScopes?: string[];
  /** ISO timestamp of the recorded scope consent, when one exists. */
  consentedAt?: string | null;
  /** Version of the consent terms agreed to; 0 = legacy row. */
  consentVersion?: number;
}

export type SettingsBanner =
  | { kind: 'linked'; provider: 'discord' }
  | { kind: 'error'; code: 'conflict' | 'lastSignInMethod' | 'stateMismatch' | 'generic' }
  | null;
/** Discord brand mark (same path as the sign-in button). */
function DiscordIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="#5865F2" aria-hidden="true" className={className}>
      <path d="M20.317 4.37a19.79 19.79 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.865-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.028C.533 9.046-.32 13.58.099 18.058a.082.082 0 0 0 .031.056 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.291.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.009c.12.099.246.198.373.293a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03ZM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418Zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418Z" />
    </svg>
  );
}

/** Google "G" brand mark. */
function GoogleIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={className}>
      <path
        fill="#4285F4"
        d="M23.49 12.27c0-.79-.07-1.54-.19-2.27H12v4.51h6.47c-.29 1.48-1.14 2.73-2.4 3.58v3h3.86c2.26-2.09 3.56-5.17 3.56-8.82Z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.86-3c-1.08.72-2.45 1.16-4.07 1.16-3.13 0-5.78-2.11-6.73-4.96H1.29v3.09C3.26 21.3 7.31 24 12 24Z"
      />
      <path
        fill="#FBBC05"
        d="M5.27 14.29c-.25-.72-.38-1.49-.38-2.29s.14-1.57.38-2.29V6.62H1.29C.47 8.24 0 10.06 0 12s.47 3.76 1.29 5.38l3.98-3.09Z"
      />
      <path
        fill="#EA4335"
        d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.31 0 3.26 2.7 1.29 6.62l3.98 3.09C6.22 6.86 8.87 4.75 12 4.75Z"
      />
    </svg>
  );
}

function UnlinkButton({ provider }: { provider: 'google' | 'discord' }) {
  const t = useTranslations('connectedAccounts');
  const [pending, setPending] = useState(false);

  async function handleUnlink() {
    setPending(true);
    try {
      const result = await unlinkProvider(provider);
      if (result.success) {
        toast.success(t(`${provider}.unlinkSuccess`));
      } else if (result.status === 'lastSignInMethod') {
        toast.error(t('errors.lastSignInMethod'));
      } else if (result.status === 'notLinked') {
        toast.error(t('errors.notLinked'));
      } else {
        toast.error(t('errors.generic'));
      }
    } catch {
      toast.error(t('errors.generic'));
    } finally {
      setPending(false);
    }
  }

  return (
    <Button
      variant="outline"
      size="sm"
      onClick={handleUnlink}
      disabled={pending}
      title={t('scopes.unlinkRevokesAll')}
    >
      {pending ? <Loader2 className="size-4 animate-spin" /> : <Unlink className="size-4" />}
      {t('unlink')}
    </Button>
  );
}

/**
 * Per-scope revoke button (issue #250): removes a single scope grant without
 * unlinking the provider. The parent card re-renders from the revalidated
 * page after the server action completes.
 */
function ScopeRevokeButton({
  provider,
  scope,
}: {
  provider: 'google' | 'discord';
  scope: string;
}) {
  const t = useTranslations('connectedAccounts');
  const [pending, setPending] = useState(false);

  async function handleRevoke() {
    setPending(true);
    try {
      const result = await revokeProviderScopes(provider, [scope]);
      if (result.success) {
        toast.success(t('scopes.revokeSuccess', { scope }));
      } else if (result.status === 'notLinked') {
        toast.error(t('errors.notLinked'));
      } else {
        toast.error(t('errors.generic'));
      }
    } catch {
      toast.error(t('errors.generic'));
    } finally {
      setPending(false);
    }
  }

  return (
    <button
      type="button"
      onClick={handleRevoke}
      disabled={pending}
      aria-label={t('scopes.revokeAriaLabel', { scope })}
      data-testid={`scope-revoke-${provider}-${scope}`}
      className="rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:opacity-50"
    >
      {pending ? <Loader2 className="size-3 animate-spin" /> : <X className="size-3" />}
    </button>
  );
}

/**
 * Consent record for one provider link (issue #250): when the consent was
 * recorded, under which terms version, and the individual scope grants — each
 * revocable on its own. Whole-provider revocation stays on the Unlink button.
 */
function ConsentSection({
  provider,
  grantedScopes,
  consentedAt,
  consentVersion,
  locale,
}: {
  provider: 'google' | 'discord';
  grantedScopes: string[];
  consentedAt: string | null;
  consentVersion: number;
  locale: string;
}) {
  const t = useTranslations('connectedAccounts');

  if (!consentedAt) {
    return null;
  }

  return (
    <div className="mt-2.5 space-y-1.5" data-testid={`connected-account-consent-${provider}`}>
      <p className="flex items-center gap-1 text-xs text-muted-foreground">
        <ShieldCheck className="size-3.5" />
        {t('scopes.consentedOn', {
          date: new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(
            new Date(consentedAt),
          ),
          version: consentVersion,
        })}
      </p>
      {grantedScopes.length > 0 ? (
        <ul className="flex flex-wrap gap-1.5" aria-label={t('scopes.grantedList')}>
          {grantedScopes.map((scope) => (
            <li
              key={scope}
              className="flex items-center gap-1 rounded-md border bg-muted/40 px-2 py-0.5 font-mono text-xs"
            >
              <span>{scope}</span>
              <ScopeRevokeButton provider={provider} scope={scope} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-muted-foreground">{t('scopes.noneGranted')}</p>
      )}
    </div>
  );
}

function ProviderRow({
  provider,
  linkedAt,
  grantedScopes,
  consentedAt,
  consentVersion,
  locale,
}: {
  provider: 'google' | 'discord';
  linkedAt: string | null;
  grantedScopes: string[];
  consentedAt: string | null;
  consentVersion: number;
  locale: string;
}) {
  const t = useTranslations('connectedAccounts');
  const Icon = provider === 'google' ? GoogleIcon : DiscordIcon;

  return (
    <div
      data-testid={`connected-account-${provider}`}
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-4"
    >
      <div className="flex items-center gap-3">
        <Icon className="size-6" />
        <div>
          <p className="font-medium leading-none">{t(`${provider}.name`)}</p>
          {linkedAt ? (
            <p className="mt-1.5 flex items-center gap-1 text-sm text-muted-foreground">
              <CheckCircle2 className="size-3.5 text-green-600 dark:text-green-400" />
              {t('linkedOn', {
                date: new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(
                  new Date(linkedAt),
                ),
              })}
            </p>
          ) : (
            <p className="mt-1.5 text-sm text-muted-foreground">{t('notLinked')}</p>
          )}
          {linkedAt && (
            <ConsentSection
              provider={provider}
              grantedScopes={grantedScopes}
              consentedAt={consentedAt}
              consentVersion={consentVersion}
              locale={locale}
            />
          )}
        </div>
      </div>
      {linkedAt ? (
        <UnlinkButton provider={provider} />
      ) : provider === 'google' ? (
        <GoogleLinkButton />
      ) : (
        <a
          href={`/api/auth/link/discord?locale=${locale}`}
          data-testid="discord-link-button"
          className={cn(buttonVariants({ variant: 'outline', size: 'sm' }))}
        >
          {t('discord.connect')}
        </a>
      )}
    </div>
  );
}

export function ConnectedAccountsCard({
  linkedAccounts,
  banner,
}: {
  linkedAccounts: LinkedAccount[];
  banner: SettingsBanner;
}) {
  const t = useTranslations('connectedAccounts');
  const locale = useLocale();

  const google = linkedAccounts.find((row) => row.provider === 'google') ?? null;
  const discord = linkedAccounts.find((row) => row.provider === 'discord') ?? null;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {banner?.kind === 'linked' && (
          <Callout type="success" title={t('banner.linkedTitle')}>
            {t('banner.linkedDiscord')}
          </Callout>
        )}
        {banner?.kind === 'error' && (
          <Callout type="error" title={t('banner.errorTitle')}>
            {t(`banner.errors.${banner.code}`)}
          </Callout>
        )}
        <ProviderRow
          provider="google"
          linkedAt={google?.linkedAt ?? null}
          grantedScopes={google?.grantedScopes ?? []}
          consentedAt={google?.consentedAt ?? null}
          consentVersion={google?.consentVersion ?? 0}
          locale={locale}
        />
        <ProviderRow
          provider="discord"
          linkedAt={discord?.linkedAt ?? null}
          grantedScopes={discord?.grantedScopes ?? []}
          consentedAt={discord?.consentedAt ?? null}
          consentVersion={discord?.consentVersion ?? 0}
          locale={locale}
        />
      </CardContent>
    </Card>
  );
}
