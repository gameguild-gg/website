import { TestingLabBulkRequestForm } from '@/components/testing-lab/testing-lab-bulk-request-form';
import { SubmitTestingBuildDialog } from '@/components/testing-lab/testing-lab-dialogs';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import { getTestingLabDashboard, getTestingProjectOptions, normalizeTestingRequestStatus } from '@/lib/testing-lab';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { ClipboardList, Filter, Search } from 'lucide-react';

type SearchParams = Promise<{ q?: string; status?: string; page?: string }>;

function pageHref(page: number, q: string, status: string) {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  if (status) params.set('status', status);
  params.set('page', String(page));
  return `/workspace/testing-lab/projects?${params}`;
}

export default async function TestingLabProjectsPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const q = params.q?.trim().toLowerCase() ?? '';
  const status = params.status?.trim() ?? '';
  const requestedPage = Math.max(1, Number(params.page) || 1);
  const pageSize = 10;
  const [directory, projects] = await Promise.all([getTestingLabDashboard(), getTestingProjectOptions()]);
  const filtered = directory.requests.filter((request) => {
    const matchesSearch = !q || `${request.title} ${request.description ?? ''}`.toLowerCase().includes(q);
    const lifecycle = request.isDeleted ? 'Archived' : normalizeTestingRequestStatus(request.status);
    const matchesStatus = !status || lifecycle === status;
    return matchesSearch && matchesStatus;
  });
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const page = Math.min(requestedPage, pageCount);
  const rows = filtered.slice((page - 1) * pageSize, page * pageSize);

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={ClipboardList}
        title="Community projects"
        description="Manage reusable project builds, testing briefs, participant capacity, feedback requirements, and project testing lifecycle."
        actions={<SubmitTestingBuildDialog projects={projects} />}
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />

      <form className="grid gap-3 rounded-md border p-3 md:grid-cols-[minmax(240px,1fr)_220px_auto]" method="get">
        <label className="relative">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input aria-label="Search community projects" name="q" defaultValue={params.q} className="pl-9" placeholder="Search title or objective" />
        </label>
        <label className="relative">
          <Filter className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <select aria-label="Filter projects by status" name="status" defaultValue={status} className="h-9 w-full rounded-md border bg-background pl-9 pr-3 text-sm">
            <option value="">All statuses</option>
            {['Draft', 'Open', 'Active', 'In Progress', 'Paused', 'Completed', 'Cancelled', 'Archived'].map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        <Button type="submit" variant="outline">
          Apply filters
        </Button>
      </form>

      {rows.length === 0 ? (
        <TestingLabEmptyState
          title={directory.requests.length === 0 ? 'No projects submitted yet' : 'No projects match these filters'}
          description={
            directory.requests.length === 0
              ? 'Submit a real platform project to prepare its first testing cycle.'
              : 'Change the search or status filter and try again.'
          }
          action={directory.requests.length === 0 ? <SubmitTestingBuildDialog projects={projects} /> : undefined}
        />
      ) : (
        <TestingLabBulkRequestForm matchingCount={filtered.length}>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[880px] text-sm">
              <thead className="bg-muted/35 text-left">
                <tr>
                  <th className="w-12 px-4 py-3">
                    <span className="sr-only">Select</span>
                  </th>
                  <th className="px-4 py-3 font-medium">Testing brief</th>
                  <th className="px-4 py-3 font-medium">Project build</th>
                  <th className="px-4 py-3 font-medium">Window</th>
                  <th className="px-4 py-3 font-medium">Testers</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((request) => {
                  const project = request.projectVersion?.project;
                  return (
                    <tr key={request.id} className="hover:bg-muted/25">
                      <td className="px-4 py-3">
                        <input type="checkbox" name="requestIds" value={request.id} className="size-4" aria-label={`Select ${request.title}`} />
                      </td>
                      <td className="max-w-80 px-4 py-3">
                        <Link href={`/workspace/testing-lab/projects/${request.id}`} className="font-medium hover:underline">
                          {request.title}
                        </Link>
                        <p className="truncate text-xs text-muted-foreground">{request.description ?? 'No objective provided'}</p>
                      </td>
                      <td className="px-4 py-3">{project?.title ?? project?.slug ?? 'Project build'}</td>
                      <td className="px-4 py-3 text-muted-foreground">
                        {request.startDate ? new Date(request.startDate).toLocaleDateString() : 'Open'} -{' '}
                        {request.endDate ? new Date(request.endDate).toLocaleDateString() : 'No deadline'}
                      </td>
                      <td className="px-4 py-3">
                        {request.currentTesterCount ?? 0}/{request.maxTesters ?? 'Unlimited'}
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant="outline">{request.isDeleted ? 'Archived' : normalizeTestingRequestStatus(request.status)}</Badge>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between border-t px-4 py-3 text-sm">
            <span className="text-muted-foreground">
              Page {page} of {pageCount}
            </span>
            <div className="flex gap-2">
              <Button asChild size="sm" variant="outline" disabled={page <= 1}>
                <Link href={pageHref(Math.max(1, page - 1), q, status)}>Previous</Link>
              </Button>
              <Button asChild size="sm" variant="outline" disabled={page >= pageCount}>
                <Link href={pageHref(page + 1, q, status)}>Next</Link>
              </Button>
            </div>
          </div>
        </TestingLabBulkRequestForm>
      )}
    </div>
  );
}
