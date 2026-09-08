import { TestingLabActionForm } from '@/components/testing-lab/testing-lab-action-form';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { rateTestingFeedback, reportTestingFeedback } from '@/lib/testing-lab/actions';
import { getTestingFeedbackDirectory } from '@/lib/testing-lab';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { MessageSquareText, Search, Star } from 'lucide-react';
import Link from 'next/link';

interface FeedbackSearchParams {
  q?: string;
  source?: 'all' | 'event' | 'request';
  reported?: 'all' | 'reported' | 'unreported';
  quality?: 'all' | 'Low' | 'Medium' | 'High';
  page?: string;
}

function pageHref(params: FeedbackSearchParams, page: number) {
  const query = Object.fromEntries(
    Object.entries({ ...params, page: String(page) }).filter(([, value]) => value && value !== 'all'),
  );
  return { pathname: '/workspace/testing-lab/feedback', query };
}

export default async function TestingLabFeedbackPage({ searchParams }: { searchParams: Promise<FeedbackSearchParams> }) {
  const params = await searchParams;
  const page = Math.max(1, Number.parseInt(params.page ?? '1', 10) || 1);
  const take = 20;
  const directory = await getTestingFeedbackDirectory({
    q: params.q,
    source: params.source ?? 'all',
    reported: params.reported === 'reported' ? true : params.reported === 'unreported' ? false : undefined,
    quality: params.quality && params.quality !== 'all' ? params.quality : undefined,
    skip: (page - 1) * take,
    take,
  });
  const totalPages = Math.max(1, Math.ceil(directory.totalCount / directory.take));

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={MessageSquareText}
        title="Testing feedback"
        description="Review every feedback submission used by active Testing Lab requests, assign quality ratings, and report unsafe or low-integrity content."
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />
      <form method="get" className="grid gap-3 md:grid-cols-[minmax(16rem,1fr)_auto_auto_auto]">
        <label className="relative">
          <span className="sr-only">Search feedback</span>
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input name="q" defaultValue={params.q} className="pl-9" placeholder="Search context, member, or feedback" />
        </label>
        <select aria-label="Filter by source" name="source" defaultValue={params.source ?? 'all'} className="h-9 rounded-md border bg-background px-3 text-sm">
          <option value="all">All sources</option>
          <option value="event">Events</option>
          <option value="request">Requests</option>
        </select>
        <select aria-label="Filter by report status" name="reported" defaultValue={params.reported ?? 'all'} className="h-9 rounded-md border bg-background px-3 text-sm">
          <option value="all">All reports</option>
          <option value="reported">Reported</option>
          <option value="unreported">Not reported</option>
        </select>
        <select aria-label="Filter by quality" name="quality" defaultValue={params.quality ?? 'all'} className="h-9 rounded-md border bg-background px-3 text-sm">
          <option value="all">All qualities</option>
          <option value="Low">Low</option>
          <option value="Medium">Medium</option>
          <option value="High">High</option>
        </select>
        <Button type="submit" className="md:col-start-4">Apply filters</Button>
      </form>
      {directory.items.length === 0 ? (
        <TestingLabEmptyState
          title={params.q ? 'No feedback matches this search' : 'No feedback submitted'}
          description="Participant feedback appears after a member completes a testing report."
        />
      ) : (
        <div className="space-y-3">
          {directory.items.map((entry) => {
            const source = entry.source === 'Event' || entry.source === 1 ? 'Event' : 'Request';
            const contextTitle = entry.eventName ?? entry.requestTitle ?? `${source} feedback`;
            return (
            <article key={entry.id} className="rounded-md border p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold">{contextTitle}</h2>
                    <Badge variant="secondary">{source}</Badge>
                    {entry.projectTitle ? <Badge variant="outline">{entry.projectTitle}{entry.projectVersion ? ` · ${entry.projectVersion}` : ''}</Badge> : null}
                    {entry.isReported ? <Badge variant="destructive">Reported</Badge> : null}
                    <Badge variant="outline">{entry.testingContext}</Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">{entry.userName ?? entry.userEmail ?? entry.userId}</p>
                </div>
                <div className="flex items-center gap-1 text-sm">
                  <Star className="size-4" />
                  {entry.overallRating ?? '-'} / 10
                </div>
              </div>
              <p className="mt-4 whitespace-pre-wrap text-sm">{entry.additionalNotes ?? entry.feedbackData}</p>
              <div className="mt-4 flex flex-col gap-3 border-t pt-3 lg:flex-row lg:items-end lg:justify-between">
                <TestingLabActionForm action={rateTestingFeedback} submitLabel="Save quality" pendingLabel="Saving..." className="flex flex-wrap items-end gap-2" actionsClassName="">
                  <input type="hidden" name="feedbackId" value={entry.id} />
                  <label className="text-xs font-medium">
                    Quality
                    <select
                      name="quality"
                      defaultValue={entry.qualityRating ?? 'Medium'}
                      className="mt-1 block h-8 rounded-md border bg-background px-2 text-sm"
                    >
                      <option>Low</option>
                      <option>Medium</option>
                      <option>High</option>
                    </select>
                  </label>
                </TestingLabActionForm>
                <TestingLabActionForm action={reportTestingFeedback} submitLabel="Report" pendingLabel="Reporting..." resetOnSuccess className="flex min-w-0 flex-1 items-end gap-2 lg:max-w-lg" actionsClassName="">
                  <input type="hidden" name="feedbackId" value={entry.id} />
                  <label className="min-w-0 flex-1 text-xs font-medium">
                    Moderation report
                    <Input name="reason" required className="mt-1 h-8" placeholder="Reason for moderator review" />
                  </label>
                </TestingLabActionForm>
              </div>
            </article>
            );
          })}
          {totalPages > 1 ? (
            <nav aria-label="Feedback pagination" className="flex items-center justify-between pt-2">
              <Button asChild variant="outline" size="sm" disabled={page <= 1}>
                <Link aria-disabled={page <= 1} href={pageHref(params, Math.max(1, page - 1))}>Previous</Link>
              </Button>
              <span className="text-sm text-muted-foreground">Page {page} of {totalPages}</span>
              <Button asChild variant="outline" size="sm" disabled={page >= totalPages}>
                <Link aria-disabled={page >= totalPages} href={pageHref(params, Math.min(totalPages, page + 1))}>Next</Link>
              </Button>
            </nav>
          ) : null}
        </div>
      )}
    </div>
  );
}
