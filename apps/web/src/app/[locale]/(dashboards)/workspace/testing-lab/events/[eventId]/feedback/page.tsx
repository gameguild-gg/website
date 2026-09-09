import { getMembers } from '@/lib/community/queries/members';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { formatEventDateTime } from '@/lib/testing-lab/event-workspace';
import { getTestingEventFeedbackReview, getTestingEventWorkspaceData } from '@/lib/testing-lab/events-queries';
import { getTestingProjectOptions } from '@/lib/testing-lab/queries';
import { Badge } from '@game-guild/ui/components/badge';
import { BarChart3, CheckCircle2, Clock3, Star } from 'lucide-react';
import { notFound } from 'next/navigation';

function feedbackDetails(value?: string | null): Array<{ label: string; value: string }> {
  if (!value) return [];
  try {
    const parsed = JSON.parse(value) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return Object.entries(parsed as Record<string, unknown>).map(([label, detail]) => ({
        label: label.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (character) => character.toUpperCase()),
        value: typeof detail === 'string' ? detail : JSON.stringify(detail),
      }));
    }
  } catch {
    // Plain-text feedback remains a supported API payload.
  }
  return [{ label: 'Feedback', value }];
}

export default async function TestingEventFeedbackPage({ params }: { params: Promise<{ eventId: string }> }) {
  const { eventId } = await params;
  const [detail, review, memberDirectory, projects] = await Promise.all([getTestingEventWorkspaceData(eventId), getTestingEventFeedbackReview(eventId), getMembers({ page: 1, limit: 100 }), getTestingProjectOptions()]);
  if (!detail.event) notFound();

  const members = new Map(memberDirectory.members.map((member) => [member.id, member.displayName || member.email || 'Unknown tester']));
  const projectTitles = new Map(projects.map((project) => [project.id, project.title]));
  const applications = new Map(detail.applications.map((application) => [application.id, application]));
  const slots = new Map(detail.slots.map((slot) => [slot.id, slot]));
  const pending = review.feedback.filter((item) => item.status === 'Pending' || !item.feedback).length;
  const submitted = review.feedback.filter((item) => Boolean(item.feedback)).length;
  const ratings = review.feedback.map((item) => item.feedback?.overallRating).filter((rating): rating is number => typeof rating === 'number');
  const averageRating = ratings.length > 0 ? (ratings.reduce((sum, rating) => sum + rating, 0) / ratings.length).toFixed(1) : null;

  return (
    <div className="space-y-5">
      <TestingLabPageHeader headingLevel={2} icon={BarChart3} title="Feedback review" description="Review every required tester submission, identify pending obligations, and preserve project feedback evidence." />

      <TestingLabAccessIssues issues={review.accessIssues} />

      <section className="grid gap-3 sm:grid-cols-3">
        <article className="rounded-md border p-4">
          <div className="flex items-center gap-2 text-muted-foreground">
            <Clock3 className="size-4" />
            <span className="text-sm">Awaiting feedback</span>
          </div>
          <p className="mt-2 text-2xl font-semibold">{pending} pending</p>
        </article>
        <article className="rounded-md border p-4">
          <div className="flex items-center gap-2 text-muted-foreground">
            <CheckCircle2 className="size-4" />
            <span className="text-sm">Received feedback</span>
          </div>
          <p className="mt-2 text-2xl font-semibold">{submitted} submitted</p>
        </article>
        <article className="rounded-md border p-4">
          <div className="flex items-center gap-2 text-muted-foreground">
            <Star className="size-4" />
            <span className="text-sm">Average rating</span>
          </div>
          <p className="mt-2 text-2xl font-semibold">{averageRating ? `${averageRating}/10` : 'No ratings'}</p>
        </article>
      </section>

      {review.feedback.length === 0 ? (
        <TestingLabEmptyState title="No feedback obligations yet" description="Assign an approved project to a checked-in tester to create a required feedback obligation." />
      ) : (
        <div className="divide-y rounded-md border">
          {review.feedback.map((item) => {
            const application = applications.get(item.applicationId);
            const projectTitle = application?.projectId ? (projectTitles.get(application.projectId) ?? 'Approved project') : 'Approved project';
            const testerName = item.testerUserId
              ? members.get(item.testerUserId) ?? 'Unknown tester'
              : 'Unknown tester';
            const slot = slots.get(item.slotId);
            const details = feedbackDetails(item.feedback?.feedbackData);

            return (
              <article key={item.obligationId} className="space-y-4 p-4">
                <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <h2 className="truncate font-semibold">{projectTitle}</h2>
                    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
                      <span>{testerName}</span>
                      {slot?.startsAt ? <span className="before:mr-2 before:content-['/']">{formatEventDateTime(slot.startsAt)}</span> : null}
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {typeof item.feedback?.overallRating === 'number' ? <Badge variant="secondary">{item.feedback.overallRating}/10</Badge> : null}
                    <Badge variant={item.feedback ? 'secondary' : 'outline'}>{item.feedback ? 'Submitted' : 'Pending'}</Badge>
                  </div>
                </header>

                {item.feedback ? (
                  <div className="grid gap-4 border-t pt-4 lg:grid-cols-[minmax(0,1fr)_minmax(14rem,0.4fr)]">
                    <dl className="grid gap-3 sm:grid-cols-2">
                      {details.map((detailItem) => (
                        <div key={detailItem.label}>
                          <dt className="text-xs font-medium text-muted-foreground">{detailItem.label}</dt>
                          <dd className="mt-1 text-sm">{detailItem.value}</dd>
                        </div>
                      ))}
                    </dl>
                    <div className="text-sm">
                      <p className="text-xs font-medium text-muted-foreground">Recommendation</p>
                      <p className="mt-1">{item.feedback.wouldRecommend === true ? 'Would recommend' : item.feedback.wouldRecommend === false ? 'Would not recommend' : 'Not provided'}</p>
                      {item.feedback.additionalNotes ? (
                        <>
                          <p className="mt-3 text-xs font-medium text-muted-foreground">Additional notes</p>
                          <p className="mt-1">{item.feedback.additionalNotes}</p>
                        </>
                      ) : null}
                    </div>
                  </div>
                ) : (
                  <p className="border-t pt-4 text-sm text-muted-foreground">This tester still needs to submit feedback before participation can be completed.</p>
                )}
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}
