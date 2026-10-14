'use client';

import { useEffect, useState } from 'react';
import { Link } from '@/i18n/navigation';
import { buttonVariants } from '@game-guild/ui/components/button';
import { ShieldAlert, X } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';

/** localStorage key holding `{"dismissedAt":"<ISO>"}` for the 2FA nudge. */
export const TWO_FACTOR_NUDGE_STORAGE_KEY = 'gg.2fa-nudge';

/** Dismissals older than 30 days re-show the banner. */
const RESHOW_AFTER_MS = 30 * 24 * 60 * 60 * 1000;

function readDismissedAt(): string | null {
  try {
    const raw = window.localStorage.getItem(TWO_FACTOR_NUDGE_STORAGE_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    if (
      typeof parsed === 'object' &&
      parsed !== null &&
      typeof (parsed as { dismissedAt?: unknown }).dismissedAt === 'string'
    ) {
      return (parsed as { dismissedAt: string }).dismissedAt;
    }
    return null;
  } catch {
    return null;
  }
}

function isDismissed(): boolean {
  const dismissedAt = readDismissedAt();
  if (!dismissedAt) return false;
  const dismissedMs = Date.parse(dismissedAt);
  if (Number.isNaN(dismissedMs)) return false;
  return Date.now() - dismissedMs < RESHOW_AFTER_MS;
}

/**
 * Persistent nudge for accounts without two-factor authentication (issue #838).
 *
 * - Hidden while `twoFactorEnabled` is true; a stale dismissal is cleared so a
 *   later disable re-arms the nudge.
 * - Dismissal is per-browser (`gg.2fa-nudge`), re-showing after 30 days.
 */
export function TwoFactorNudgeBanner({ twoFactorEnabled }: { twoFactorEnabled: boolean }) {
  const t = useTranslations('settings.security');
  const [dismissed, setDismissed] = useState(true);

  useEffect(() => {
    if (twoFactorEnabled) {
      // Re-arm: enabled accounts clear any stale dismissal so a future
      // disable re-nudges instead of honoring an old dismissal.
      window.localStorage.removeItem(TWO_FACTOR_NUDGE_STORAGE_KEY);
      return;
    }
    setDismissed(isDismissed());
  }, [twoFactorEnabled]);

  if (twoFactorEnabled || dismissed) return null;

  return (
    <div
      role="status"
      data-testid="two-factor-nudge-banner"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-yellow-200 bg-yellow-50 p-4 text-yellow-800 dark:border-yellow-900 dark:bg-yellow-950/50 dark:text-yellow-200"
    >
      <div className="flex items-start gap-3">
        <ShieldAlert className="mt-0.5 size-5 flex-shrink-0" aria-hidden="true" />
        <p className="text-sm font-medium">{t('nudge.title')}</p>
      </div>
      <div className="flex items-center gap-2">
        <Link
          href="/workspace/settings/security"
          className={cn(buttonVariants({ size: 'sm' }))}
          data-testid="two-factor-nudge-action"
        >
          {t('nudge.action')}
        </Link>
        <button
          type="button"
          onClick={() => {
            window.localStorage.setItem(
              TWO_FACTOR_NUDGE_STORAGE_KEY,
              JSON.stringify({ dismissedAt: new Date().toISOString() }),
            );
            setDismissed(true);
          }}
          aria-label={t('nudge.dismiss')}
          data-testid="two-factor-nudge-dismiss"
          className="rounded-sm p-1 transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          <X className="size-4" aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}
