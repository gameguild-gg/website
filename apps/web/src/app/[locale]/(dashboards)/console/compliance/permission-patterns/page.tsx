import { PermissionPatternConsole } from '@/components/compliance/permission-pattern-console';
import { buildPermissionChangeQueryText, buildPermissionPatternDataset, normalizePermissionWindowDays, orderPermissionAlertsFirst, type PermissionPatternsAlertPayload, type PermissionPatternsQueryPayload } from '@/lib/audit/permission-patterns';
import { redirect } from '@/i18n/navigation';
import { getRequestAuthContext, auth } from '@/auth';

/** Server-side data loader for the permission-pattern dashboard (issue #297). The
 * caller's session token authorizes both reads; audit query authorization, data masking
 * and alert scoping remain enforced on the API. The console refetches on window change. */
async function loadPermissionPatterns(days: number, token: string): Promise<{
  query: PermissionPatternsQueryPayload;
  alerts: PermissionPatternsAlertPayload[];
} | null> {
  const baseUrl = (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080').replace(/\/$/, '');
  const headers = { authorization: `Bearer ${token}`, accept: 'application/json', 'content-type': 'application/json' };
  const queryText = buildPermissionChangeQueryText(days);
  const queryFetch = fetch(`${baseUrl}/v1/audit-queries/:execute`, {
    method: 'POST',
    headers,
    cache: 'no-store',
    body: JSON.stringify({ queryText, skip: 0, take: 200 }),
  });
  const alertsFetch = fetch(`${baseUrl}/api/audit/security-events/alerts?take=50`, { headers, cache: 'no-store' });
  const queryResult = await queryFetch;
  if (!queryResult.ok) return null;
  const queryPayload: unknown = await queryResult.json().catch(() => null);
  const alertsResult = await alertsFetch;
  const alertsPayload: unknown = alertsResult.ok ? await alertsResult.json().catch(() => null) : null;
  return {
    query: queryPayload ?? {},
    alerts: Array.isArray(alertsPayload) ? (alertsPayload as PermissionPatternsAlertPayload[]) : [],
  };
}

/**
 * Permission-change pattern dashboard (issue #297): the compliance console surface that
 * visualizes how permissions change over time — volume trends, action mix, most active
 * actors and the permission-rule alert queue. Session authentication is enforced here.
 */
export default async function PermissionPatternsPage({
  params,
}: { params: Promise<{ locale: string }> }): Promise<React.JSX.Element> {
  const { locale } = await params;
  const session = await auth();
  if (!session || typeof session === 'function') {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/console/compliance/permission-patterns' } }, locale });
    throw new Error('Unauthenticated console access');
  }
  const { token } = (await getRequestAuthContext()) as { token: string | null };
  if (!token) {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/console/compliance/permission-patterns' } }, locale });
    throw new Error('Unauthenticated console access');
  }

  const days = normalizePermissionWindowDays(30);
  const initial = await loadPermissionPatterns(days, token);
  const dataset = initial ? buildPermissionPatternDataset(initial.query) : null;
  const alerts = initial ? orderPermissionAlertsFirst(initial.alerts) : [];

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">Permission change patterns</h1>
        <p className="text-muted-foreground">
          How permissions move in this tenant: grant/revoke volume over time, the action mix, the actors behind
          the most mutations, and the alerts raised for suspicious patterns and bulk operations.
        </p>
      </header>
      {dataset === null ? (
        <p className="text-sm text-muted-foreground">
          The audit store rejected the initial dashboard query. Try a different window or retry shortly.
        </p>
      ) : (
        <PermissionPatternConsole initialDays={days} initialDataset={dataset} initialAlerts={alerts} />
      )}
    </div>
  );
}
