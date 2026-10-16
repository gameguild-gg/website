'use client';

import { Loader2, RefreshCw, ShieldCheck, ShieldAlert } from 'lucide-react';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { useAuthState } from '@/hooks/use-auth-state';

function formatUtc(value: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toISOString().replace('T', ' ').slice(0, 19) + ' UTC';
}

/** Account-settings surface for the session-state endpoint (issue #221): makes the
 * authentication infrastructure user-visible — session validity, expiry, token version,
 * active sessions and MFA enrollment. */
export function SessionStateCard() {
  const { state, error, loading, refresh } = useAuthState();

  return (
    <Card data-testid="session-state-card">
      <CardHeader>
        <CardTitle>Session state</CardTitle>
        <CardDescription>Live view of your current authentication session.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading && !state ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" aria-hidden /> Loading session state…
          </p>
        ) : null}
        {error ? (
          <p className="text-destructive text-sm" data-testid="session-state-error">
            {error}
          </p>
        ) : null}
        {state ? (
          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2" data-testid="session-state-fields">
            <div className="flex items-center gap-2">
              <dt className="text-muted-foreground">Session</dt>
              <dd className="flex items-center gap-1 font-medium">
                {state.sessionValid ? (
                  <>
                    <ShieldCheck className="size-4 text-primary" aria-hidden /> Valid
                  </>
                ) : (
                  <>
                    <ShieldAlert className="size-4 text-destructive" aria-hidden /> Not valid
                  </>
                )}
              </dd>
            </div>
            <div>
              <dt className="text-muted-foreground inline">Expires </dt>
              <dd className="inline font-mono text-xs">{formatUtc(state.sessionExpiresAtUtc)}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground inline">Token version </dt>
              <dd className="inline font-mono text-xs">{state.tokenVersion ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground inline">Active sessions </dt>
              <dd className="inline font-mono text-xs">{state.activeSessionCount}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground inline">MFA enrollment </dt>
              <dd className="inline font-medium">{state.mfaEnrolled ? 'Enrolled' : 'Not enrolled'}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground inline">Last used </dt>
              <dd className="inline font-mono text-xs">{formatUtc(state.sessionLastUsedAtUtc)}</dd>
            </div>
            {state.sessionTerminationReason ? (
              <div className="sm:col-span-2">
                <dt className="text-muted-foreground inline">Termination reason </dt>
                <dd className="inline font-medium">{state.sessionTerminationReason}</dd>
              </div>
            ) : null}
          </dl>
        ) : null}
        <Button variant="outline" size="sm" onClick={() => void refresh()} disabled={loading}>
          <RefreshCw className="size-4" aria-hidden /> Refresh
        </Button>
      </CardContent>
    </Card>
  );
}
