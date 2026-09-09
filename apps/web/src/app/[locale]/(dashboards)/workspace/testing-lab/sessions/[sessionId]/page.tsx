import { TestingLabActionForm } from '@/components/testing-lab/testing-lab-action-form';
import { TestingLabConfirmAction } from '@/components/testing-lab/testing-lab-confirm-action';
import { EditTestingSessionDialog, LinkTestingSessionProjectDialog } from '@/components/testing-lab/testing-lab-dialogs';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import {
  deleteTestingSession,
  restoreTestingSession,
  unlinkTestingSessionProject,
  updateTestingAttendance,
} from '@/lib/testing-lab/actions';
import { getTestingLabDashboard, getTestingProjectOptions, getTestingSessionDetail, normalizeTestingSessionStatus } from '@/lib/testing-lab';
import { getMembers } from '@/lib/community/queries/members';
import { Badge } from '@game-guild/ui/components/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { CalendarDays } from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingSessionDetailPage({ params }: { params: Promise<{ sessionId: string }> }) {
  const { sessionId } = await params;
  const [detail, projectOptions, directory, memberDirectory] = await Promise.all([
    getTestingSessionDetail(sessionId),
    getTestingProjectOptions(),
    getTestingLabDashboard(),
    getMembers({ page: 1, limit: 100 }),
  ]);
  if (!detail.session && detail.accessIssues.length === 0) notFound();
  const session = detail.session;
  if (!session)
    return (
      <div className="p-6">
        <TestingLabAccessIssues issues={detail.accessIssues} />
      </div>
    );

  const memberLabels = new Map(
    memberDirectory.members.map((member) => [member.id, member.displayName || member.email || 'Unknown tester']),
  );
  const memberLabel = (userId?: string | null) => userId ? memberLabels.get(userId) : undefined;
  const linkedProjectIds = new Set(detail.projects.map((project) => project.projectId).filter(Boolean));
  const availableProjects = projectOptions.filter((project) => !linkedProjectIds.has(project.id));

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={CalendarDays}
        title={session.sessionName}
        description={`${session.sessionDate ? new Date(session.sessionDate).toLocaleDateString() : 'Date pending'} at ${session.location?.name ?? 'an unassigned location'}.`}
        actions={
          <>
            <LinkTestingSessionProjectDialog sessionId={session.id} projects={availableProjects} />
            <EditTestingSessionDialog session={session} locations={directory.locations} />
            <TestingLabConfirmAction
              action={session.isDeleted ? restoreTestingSession : deleteTestingSession}
              fields={{ sessionId: session.id }}
              label={session.isDeleted ? 'Restore' : 'Archive'}
              title={session.isDeleted ? 'Restore this testing session?' : 'Archive this testing session?'}
              description={
                session.isDeleted
                  ? 'The session will return to the operational schedule.'
                  : 'Registration will stop and the session will leave active schedules. It can be restored later.'
              }
              confirmLabel={session.isDeleted ? 'Restore session' : 'Archive session'}
              intent={session.isDeleted ? 'restore' : 'archive'}
            />
          </>
        }
      />
      <TestingLabAccessIssues issues={detail.accessIssues} />

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Status</CardTitle>
          </CardHeader>
          <CardContent>
            <Badge>{normalizeTestingSessionStatus(session.status)}</Badge>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Registrations</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">
            {detail.registrations.length}/{session.maxTesters ?? 0}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Waitlist</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{detail.waitlist.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Projects</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{detail.projects.filter((project) => project.isActive !== false).length}</CardContent>
        </Card>
      </section>

      <section className="space-y-6">
          <div>
            <h2 className="mb-3 text-lg font-semibold">Registrations and attendance</h2>
            {detail.registrations.length === 0 ? (
              <TestingLabEmptyState title="No registrations" description="Eligible members can register from the public Testing Lab." />
            ) : (
              <div className="divide-y rounded-md border">
                {detail.registrations.map((registration) => (
                  <div key={registration.id ?? registration.userId} className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <p className="text-sm font-medium">{registration.user?.name ?? registration.user?.email ?? memberLabel(registration.userId) ?? 'Unknown tester'}</p>
                      <p className="text-xs text-muted-foreground">
                        {registration.registrationType ?? 'Tester'} · {registration.status ?? 'Registered'}
                      </p>
                    </div>
                    <TestingLabActionForm action={updateTestingAttendance} submitLabel="Save" pendingLabel="Saving..." className="flex flex-wrap items-center gap-2" actionsClassName="">
                      <input type="hidden" name="sessionId" value={session.id} />
                      <input type="hidden" name="userId" value={registration.userId} />
                      <Select name="attendanceStatus" defaultValue={registration.attendanceStatus ?? 'Registered'}>
                        <SelectTrigger className="h-8 min-w-32 text-xs">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {['Registered', 'Present', 'Completed', 'NoShow'].map((value) => (
                            <SelectItem key={value} value={value}>{value}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </TestingLabActionForm>
                  </div>
                ))}
              </div>
            )}
          </div>
          <div>
            <h2 className="mb-3 text-lg font-semibold">Waitlist</h2>
            {detail.waitlist.length === 0 ? (
              <p className="rounded-md border border-dashed p-5 text-sm text-muted-foreground">No members are waiting for a seat.</p>
            ) : (
              <div className="divide-y rounded-md border">
                {detail.waitlist.map((entry) => (
                  <div key={entry.id ?? entry.userId} className="flex justify-between p-3 text-sm">
                    <span>{entry.user?.name ?? entry.user?.email ?? memberLabel(entry.userId) ?? 'Unknown tester'}</span>
                    <Badge variant="outline">Position {entry.position}</Badge>
                  </div>
                ))}
              </div>
            )}
          </div>
          <div>
            <h2 className="mb-3 text-lg font-semibold">Linked projects</h2>
            {detail.projects.length === 0 ? (
              <p className="rounded-md border border-dashed p-5 text-sm text-muted-foreground">No projects linked to this session.</p>
            ) : (
              <div className="divide-y rounded-md border">
                {detail.projects.map((project) => (
                  <div key={project.linkId ?? project.projectId} className="flex items-center justify-between gap-3 p-3 text-sm">
                    <span>{projectOptions.find((option) => option.id === project.projectId)?.title ?? project.projectId}</span>
                    <div className="flex items-center gap-2">
                      <Badge variant="outline">{project.isActive === false ? 'Inactive' : 'Active'}</Badge>
                      {project.projectId ? (
                        <TestingLabConfirmAction
                          action={unlinkTestingSessionProject}
                          fields={{ sessionId: session.id, projectId: project.projectId }}
                          label="Unlink"
                          title="Unlink this project?"
                          description="The project leaves this testing session. Its project record and previous evidence remain intact."
                          confirmLabel="Unlink project"
                          intent="delete"
                        />
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
      </section>
    </div>
  );
}
