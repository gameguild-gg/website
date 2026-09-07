import { CreateTestingSessionDialog } from '@/components/testing-lab/testing-lab-dialogs';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import { getTestingLabDashboard, normalizeTestingSessionStatus } from '@/lib/testing-lab';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { CalendarDays, Search } from 'lucide-react';

export default async function TestingLabSessionsPage({ searchParams }: { searchParams: Promise<{ q?: string; status?: string; page?: string }> }) {
  const params = await searchParams;
  const q = params.q?.trim().toLowerCase() ?? '';
  const status = params.status?.trim() ?? '';
  const requestedPage = Math.max(1, Number(params.page) || 1);
  const pageSize = 10;
  const directory = await getTestingLabDashboard();
  const filtered = directory.sessions.filter((session) => {
    const matchesSearch = !q || `${session.sessionName} ${session.location?.name ?? ''}`.toLowerCase().includes(q);
    const matchesStatus = !status || normalizeTestingSessionStatus(session.status) === status;
    return matchesSearch && matchesStatus;
  });
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const page = Math.min(requestedPage, pageCount);
  const rows = filtered.slice((page - 1) * pageSize, page * pageSize);

  function href(nextPage: number) {
    const next = new URLSearchParams();
    if (q) next.set('q', q);
    if (status) next.set('status', status);
    next.set('page', String(nextPage));
    return `/workspace/testing-lab/sessions?${next}`;
  }

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={CalendarDays}
        title="Testing sessions"
        description="Schedule moderated testing windows and manage capacity, linked projects, registrations, waitlists, and attendance."
        actions={<CreateTestingSessionDialog requests={directory.requests} locations={directory.locations} />}
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />
      <form method="get" className="grid gap-3 rounded-md border p-3 md:grid-cols-[minmax(240px,1fr)_220px_auto]">
        <label className="relative">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input name="q" defaultValue={params.q} className="pl-9" placeholder="Search session or location" />
        </label>
        <select name="status" defaultValue={status} className="h-9 rounded-md border bg-background px-3 text-sm">
          <option value="">All statuses</option>
          {['Scheduled', 'Active', 'Completed', 'Cancelled'].map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
        <Button type="submit" variant="outline">
          Apply filters
        </Button>
      </form>

      {rows.length === 0 ? (
        <TestingLabEmptyState
          title={directory.sessions.length === 0 ? 'No testing sessions' : 'No sessions match these filters'}
          description={
            directory.sessions.length === 0 ? 'Schedule a moderated testing window for an active request.' : 'Change the search or status filter and try again.'
          }
          action={directory.sessions.length === 0 ? <CreateTestingSessionDialog requests={directory.requests} locations={directory.locations} /> : undefined}
        />
      ) : (
        <div className="overflow-hidden rounded-md border">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[860px] text-sm">
              <thead className="bg-muted/35 text-left">
                <tr>
                  <th className="px-4 py-3 font-medium">Session</th>
                  <th className="px-4 py-3 font-medium">Date</th>
                  <th className="px-4 py-3 font-medium">Location</th>
                  <th className="px-4 py-3 font-medium">Testers</th>
                  <th className="px-4 py-3 font-medium">Projects</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((session) => (
                  <tr key={session.id} className="hover:bg-muted/25">
                    <td className="px-4 py-3">
                      <Link href={`/workspace/testing-lab/sessions/${session.id}`} className="font-medium hover:underline">
                        {session.sessionName}
                      </Link>
                    </td>
                    <td className="px-4 py-3">{session.sessionDate ? new Date(session.sessionDate).toLocaleDateString() : 'Not scheduled'}</td>
                    <td className="px-4 py-3">{session.location?.name ?? 'Unassigned'}</td>
                    <td className="px-4 py-3">
                      {session.registeredTesterCount ?? 0}/{session.maxTesters ?? 0}
                    </td>
                    <td className="px-4 py-3">{session.registeredProjectCount ?? 0}</td>
                    <td className="px-4 py-3">
                      <Badge variant="outline">{normalizeTestingSessionStatus(session.status)}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between border-t px-4 py-3 text-sm">
            <span className="text-muted-foreground">
              Page {page} of {pageCount}
            </span>
            <div className="flex gap-2">
              <Button asChild size="sm" variant="outline" disabled={page <= 1}>
                <Link href={href(Math.max(1, page - 1))}>Previous</Link>
              </Button>
              <Button asChild size="sm" variant="outline" disabled={page >= pageCount}>
                <Link href={href(page + 1)}>Next</Link>
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
