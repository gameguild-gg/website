import { AuditReplayConsole } from '@/components/compliance/audit-replay-console';
import { redirect } from '@/i18n/navigation';
import { auth } from '@/auth';

/**
 * Audit stream replay console (issue #169): the minimal administrator surface that wires
 * the landed web replay core to the guarded capture and reconstruction endpoints. Session
 * authentication is enforced here; snapshot authorization, durable access auditing and
 * integrity digests remain server-side on the API.
 */
export default async function AuditReplayPage({
  params,
}: { params: Promise<{ locale: string }> }): Promise<React.JSX.Element> {
  const [{ locale }, session] = await Promise.all([params, auth()]);
  if (!session || typeof session === 'function') {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/console/compliance/audit-replay' } }, locale });
    throw new Error('Unauthenticated console access');
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">Audit stream replay</h1>
        <p className="text-muted-foreground">
          Incident investigation over frozen audit snapshots: capture a range, verify its digest, and replay the
          reconstructed timeline with per-session journeys.
        </p>
      </header>
      <AuditReplayConsole />
    </div>
  );
}
