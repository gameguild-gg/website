import { cn } from "@game-guild/ui/lib/utils";
import { buttonVariants } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { AlertTriangle, CheckCircle2, MailX } from 'lucide-react';
import React from 'react';

/**
 * Public unsubscribe landing. Email footers link here with `?token=`; the token
 * is exchanged for a result SERVER-SIDE (this async server component) so it
 * never reaches client JS. The page also renders directly from
 * `?status=&scope=&value=` query params for callers that already hold a result.
 * Hardcoded English by design — matches workspace-shell.tsx (no i18n seam yet).
 */

type UnsubscribeState = 'success' | 'already' | 'invalid';
type UnsubscribeScope = 'type' | 'category' | 'all';

interface UnsubscribeView {
  state: UnsubscribeState;
  scope: UnsubscribeScope;
  value: string | null;
}

const MANAGE_PREFERENCES_PATH = '/workspace/settings/notifications';

function firstParam(param: string | string[] | undefined): string | undefined {
  return Array.isArray(param) ? param[0] : param;
}

// Any unknown/missing combination collapses to the invalid state — the page
// must never crash or leak whether a token ever existed.
function viewFromParams(
  status: string | undefined,
  scope: string | undefined,
  value: string | undefined,
): UnsubscribeView {
  if (status !== 'success' && status !== 'already') {
    return { state: 'invalid', scope: 'all', value: null };
  }
  if (scope === 'all') {
    return { state: status, scope: 'all', value: null };
  }
  if ((scope === 'type' || scope === 'category') && value && value.trim()) {
    return { state: status, scope, value: value.trim() };
  }
  return { state: 'invalid', scope: 'all', value: null };
}

interface UnsubscribeApiResponse {
  status?: string;
  scope?: string;
  value?: string | null;
}

// Server-side only: the DataProtection token is sent to the API and never
// rendered, logged, or shipped to the browser bundle.
async function viewFromToken(token: string): Promise<UnsubscribeView> {
  const apiUrl = (process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL) || 'http://localhost:8080';
  const endpoint = `${apiUrl}/api/v1/notifications/unsubscribe?token=${encodeURIComponent(token)}`;
  try {
    const response = await fetch(endpoint, {
      headers: { Accept: 'application/json' },
      cache: 'no-store',
    });
    if (!response.ok) {
      // 400 invalid token / 422 transactional scope — both render as invalid.
      return { state: 'invalid', scope: 'all', value: null };
    }
    const data = (await response.json()) as UnsubscribeApiResponse;
    if (data.status !== 'unsubscribed') {
      return { state: 'invalid', scope: 'all', value: null };
    }
    return viewFromParams('success', data.scope ?? undefined, data.value ?? undefined);
  } catch {
    // Network/API failure: safest copy is the generic invalid state.
    return { state: 'invalid', scope: 'all', value: null };
  }
}

// "MonthlyStatement" -> "Monthly statement"; "marketing" -> "Marketing".
function humanizeValue(value: string): string {
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function successDescription(scope: UnsubscribeScope, value: string | null): string {
  if (scope === 'all') {
    return "You've been unsubscribed from all email notifications. Transactional emails such as password resets are always delivered.";
  }
  return `You've been unsubscribed from ${humanizeValue(value ?? '')} emails.`;
}

export default async function Page({
  params,
  searchParams,
}: PageProps<'/[locale]/unsubscribe'>): Promise<React.JSX.Element> {
  const [, query] = await Promise.all([params, searchParams]);
  const token = firstParam(query?.token);

  const view = token
    ? await viewFromToken(token)
    : viewFromParams(firstParam(query?.status), firstParam(query?.scope), firstParam(query?.value));

  return (
    <main className="mx-auto flex w-full max-w-md flex-col items-center px-4 py-24">
      <Card className="w-full">
        <CardHeader className="items-center text-center">
          {view.state === 'success' ? (
            <CheckCircle2 className="mb-2 h-12 w-12 text-green-600" aria-hidden="true" />
          ) : view.state === 'already' ? (
            <MailX className="mb-2 h-12 w-12 text-muted-foreground" aria-hidden="true" />
          ) : (
            <AlertTriangle className="mb-2 h-12 w-12 text-amber-500" aria-hidden="true" />
          )}
          <h1 className="text-2xl font-semibold leading-none">
            {view.state === 'success'
              ? 'You are unsubscribed'
              : view.state === 'already'
                ? 'You were already unsubscribed'
                : 'This link is invalid or expired'}
          </h1>
          <CardDescription>
            {view.state === 'success'
              ? successDescription(view.scope, view.value)
              : view.state === 'already'
                ? 'Your email preferences were already updated, so nothing has changed.'
                : 'Unsubscribe links only work from the original email. Open a recent email and use its unsubscribe link, or manage your preferences below.'}
          </CardDescription>
        </CardHeader>
        <CardContent className="flex justify-center">
          <Link href={MANAGE_PREFERENCES_PATH} className={cn(buttonVariants({}))}>
            Manage all notification preferences
          </Link>
        </CardContent>
      </Card>
    </main>
  );
}
