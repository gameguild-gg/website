import { TestingLabConfirmAction } from '@/components/testing-lab/testing-lab-confirm-action';
import { AddTestingParticipantDialog, EditTestingRequestDialog } from '@/components/testing-lab/testing-lab-dialogs';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import { deleteTestingRequest, removeTestingParticipant, restoreTestingRequest } from '@/lib/testing-lab/actions';
import { getMembers } from '@/lib/community/queries/members';
import { getTestingLabProjectDetail, normalizeTestingRequestStatus } from '@/lib/testing-lab';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { ClipboardList, Download } from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingProjectDetailPage({ params }: { params: Promise<{ projectId: string }> }) {
  const { projectId } = await params;
  const [detail, memberDirectory] = await Promise.all([getTestingLabProjectDetail(projectId), getMembers({ page: 1, limit: 100 })]);
  if (!detail.project && !detail.request && detail.accessIssues.length === 0) notFound();
  const request = detail.request;
  if (!request) {
    return (
      <div className="space-y-6 p-4 lg:p-6">
        <TestingLabPageHeader
          icon={ClipboardList}
          title={detail.project?.title ?? 'Project'}
          description={detail.project?.description ?? 'Shared project available for Testing Lab planning.'}
          actions={
            <Button asChild variant="outline">
              <Link href="/workspace/testing-lab/events">Browse testing events</Link>
            </Button>
          }
        />
        <TestingLabAccessIssues issues={detail.accessIssues} />
        {detail.project ? (
          <>
            <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Project status</CardTitle>
                </CardHeader>
                <CardContent>
                  <Badge variant="outline">{String(detail.project.status ?? 'Draft')}</Badge>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Testing Lab</CardTitle>
                </CardHeader>
                <CardContent className="text-sm text-muted-foreground">Not submitted</CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Development</CardTitle>
                </CardHeader>
                <CardContent className="text-sm text-muted-foreground">{detail.project.developmentStatus ?? 'Not specified'}</CardContent>
              </Card>
            </section>
            <TestingLabEmptyState
              title="Not submitted to Testing Lab yet"
              description="This shared project has no Testing Lab brief or event application. Choose an event to submit the project for manager review."
              action={
                <Button asChild>
                  <Link href="/workspace/testing-lab/events">Choose a testing event</Link>
                </Button>
              }
            />
          </>
        ) : null}
      </div>
    );
  }
  const status = normalizeTestingRequestStatus(request.status);
  const memberLabels = new Map(
    memberDirectory.members.map((member) => [member.id, member.displayName || member.email || 'Unknown member']),
  );

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={ClipboardList}
        title={request.title}
        description={request.description ?? 'Project testing brief and operational follow-up.'}
        actions={
          <>
            {!request.isDeleted ? (
              <>
                <AddTestingParticipantDialog requestId={request.id} members={memberDirectory.members} />
                <EditTestingRequestDialog request={request} />
              </>
            ) : null}
            {request.downloadUrl ? (
              <Button asChild variant="outline">
                <a href={request.downloadUrl}>
                  <Download className="mr-2 size-4" />
                  Open build
                </a>
              </Button>
            ) : null}
            <TestingLabConfirmAction
              action={request.isDeleted ? restoreTestingRequest : deleteTestingRequest}
              fields={{ requestId: request.id }}
              label={request.isDeleted ? 'Restore' : 'Archive'}
              title={request.isDeleted ? 'Restore this testing request?' : 'Archive this testing request?'}
              description={
                request.isDeleted
                  ? 'The request will return to active operations.'
                  : 'The request will be hidden from active operations and can be restored later.'
              }
              confirmLabel={request.isDeleted ? 'Restore request' : 'Archive request'}
              intent={request.isDeleted ? 'restore' : 'archive'}
              successHref="/workspace/testing-lab/projects"
            />
          </>
        }
      />
      <TestingLabAccessIssues issues={detail.accessIssues} />
      {request.isDeleted ? (
        <div className="rounded-md border border-dashed p-4">
          <p className="font-medium">Archived request</p>
          <p className="text-sm text-muted-foreground">
            This record is read-only. Restore it before changing participants, sessions, or request details.
          </p>
        </div>
      ) : null}

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Status</CardTitle>
          </CardHeader>
          <CardContent>
            <Badge>{status}</Badge>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Participants</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{detail.participants.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Sessions</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{detail.sessions.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Feedback</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{detail.feedback.length}</CardContent>
        </Card>
      </section>

      <section className="space-y-6">
          <div>
            <h2 className="mb-3 text-lg font-semibold">Testing sessions</h2>
            {detail.sessions.length === 0 ? (
              <TestingLabEmptyState title="No sessions yet" description="Schedule a moderated session from the Sessions workspace." />
            ) : (
              <div className="divide-y rounded-md border">
                {detail.sessions.map((session) => (
                  <Link
                    key={session.id}
                    href={`/workspace/testing-lab/sessions/${session.id}`}
                    className="flex items-center justify-between p-3 hover:bg-muted/30"
                  >
                    <span className="font-medium">{session.sessionName}</span>
                    <Badge variant="outline">{session.status}</Badge>
                  </Link>
                ))}
              </div>
            )}
          </div>
          <div>
            <h2 className="mb-3 text-lg font-semibold">Participants</h2>
            {detail.participants.length === 0 ? (
              <TestingLabEmptyState title="No participants" description="Add a member by user id or let eligible members join the request." />
            ) : (
              <div className="divide-y rounded-md border">
                {detail.participants.map((participant) => (
                  <div key={participant.id ?? participant.userId} className="flex items-center justify-between gap-3 p-3">
                    <div>
                      <p className="text-sm font-medium">{participant.user?.name ?? participant.user?.email ?? memberLabels.get(participant.userId) ?? 'Unknown member'}</p>
                      <p className="text-xs text-muted-foreground">{participant.timeSpentMinutes ?? 0} tracked minutes</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant="outline">{participant.status ?? 'Registered'}</Badge>
                      {!request.isDeleted ? (
                        <TestingLabConfirmAction
                          action={removeTestingParticipant}
                          fields={{ requestId: request.id, userId: participant.userId }}
                          label="Remove"
                          title="Remove this participant?"
                          description="The member will lose access to this testing request. Their previous feedback remains auditable."
                          confirmLabel="Remove participant"
                          intent="delete"
                        />
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
          <div>
            <h2 className="mb-3 text-lg font-semibold">Feedback</h2>
            {detail.feedback.length === 0 ? (
              <TestingLabEmptyState title="No feedback yet" description="Feedback appears here after participants submit their testing report." />
            ) : (
              <div className="space-y-3">
                {detail.feedback.map((feedback) => (
                  <div key={feedback.id} className="rounded-md border p-4">
                    <div className="flex justify-between gap-4">
                      <p className="font-medium">{feedback.user?.name ?? memberLabels.get(feedback.userId) ?? 'Unknown member'}</p>
                      <span className="text-sm">{feedback.overallRating ?? '-'} / 5</span>
                    </div>
                    <p className="mt-2 whitespace-pre-wrap text-sm text-muted-foreground">{feedback.additionalNotes ?? feedback.feedbackData}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
      </section>
    </div>
  );
}
