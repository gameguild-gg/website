import { getMembers } from '@/lib/community/queries/members';
import { TestingSlotRegistrations, type TestingLabApprovedApplicationOption } from '@/components/testing-lab/testing-event-management';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { formatEventDateTime, isTestingEventReadOnly } from '@/lib/testing-lab/event-workspace';
import { getTestingApplicationTesterEligibility, getTestingEventWorkspaceData } from '@/lib/testing-lab/events-queries';
import { getTestingProjectOptions } from '@/lib/testing-lab/queries';
import { Badge } from '@game-guild/ui/components/badge';
import { UsersRound } from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingEventTestersPage({ params }: { params: Promise<{ eventId: string }> }) {
  const { eventId } = await params;
  const [detail, memberDirectory, projects] = await Promise.all([getTestingEventWorkspaceData(eventId), getMembers({ page: 1, limit: 100 }), getTestingProjectOptions()]);
  if (!detail.event) notFound();

  const testerUserIds = [...new Set(
    Object.values(detail.registrationsBySlot)
      .flat()
      .map((registration) => registration.userId)
      .filter((userId): userId is string => Boolean(userId)),
  )];
  const testerEligibility = await getTestingApplicationTesterEligibility(eventId, testerUserIds);
  const eligibleTesterIdsByApplication = new Map<string, string[]>();
  testerEligibility.eligibility.forEach(({ testerUserId, eligibleApplicationIds }) => {
    if (!testerUserId) return;
    (eligibleApplicationIds ?? []).forEach((applicationId) => {
      const testerIds = eligibleTesterIdsByApplication.get(applicationId) ?? [];
      testerIds.push(testerUserId);
      eligibleTesterIdsByApplication.set(applicationId, testerIds);
    });
  });

  const readOnly = isTestingEventReadOnly(detail.event);
  const terminalStatuses = new Set(['Cancelled', 'Completed', 'NoShow']);
  const isActiveRegistration = (status?: string | null) => !terminalStatuses.has(status ?? '');
  const total = Object.values(detail.registrationsBySlot)
    .flat()
    .filter((registration) => isActiveRegistration(registration.status)).length;
  const memberLabels = Object.fromEntries(memberDirectory.members.map((member) => [member.id, member.displayName || member.email || 'Unknown tester']));
  const projectLabels = new Map(projects.map((project) => [project.id, project.title]));
  const approvedApplications: TestingLabApprovedApplicationOption[] = detail.applications
    .filter(
      (
        application,
      ): application is typeof application & {
        id: string;
        projectId: string;
      } => application.status === 'Approved' && Boolean(application.id) && Boolean(application.projectId),
    )
    .map((application) => ({
      id: application.id,
      slotId: application.assignedSlotId,
      label: projectLabels.get(application.projectId) ?? 'Approved project',
      eligibleTesterUserIds: eligibleTesterIdsByApplication.get(application.id) ?? [],
    }));

  return (
    <div className="space-y-5">
      <TestingLabPageHeader headingLevel={2} icon={UsersRound} title="Testers and attendance" description="Manage tester participation per slot and preserve check-in, check-out, no-show, and completion evidence." />

      <p className="text-sm text-muted-foreground">{total === 1 ? '1 tester registered across this event.' : `${total} testers registered across this event.`}</p>
      {testerEligibility.accessIssues.length > 0 ? (
        <p role="alert" className="text-sm text-destructive">
          Project eligibility could not be loaded. Assignment is disabled to prevent conflicts of interest.
        </p>
      ) : null}

      {detail.slots.length === 0 ? (
        <div className="rounded-md border border-dashed p-8 text-center">
          <h2 className="font-medium">No tester schedule available</h2>
          <p className="mt-1 text-sm text-muted-foreground">Create event slots before accepting tester registrations.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {detail.slots.map((slot) => {
            const registrations = slot.id ? (detail.registrationsBySlot[slot.id] ?? []) : [];
            const activeRegistrationCount = registrations.filter((registration) =>
              isActiveRegistration(registration.status),
            ).length;
            return (
              <section key={slot.id} className="rounded-md border p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="font-semibold">{formatEventDateTime(slot.startsAt)}</h2>
                    <p className="text-sm text-muted-foreground">{slot.campusName ?? slot.meetingUrl ?? slot.mode}</p>
                  </div>
                  <Badge variant="outline">{activeRegistrationCount} registered</Badge>
                </div>
                <TestingSlotRegistrations eventId={eventId} registrations={registrations} memberLabels={memberLabels} approvedApplications={approvedApplications} readOnly={readOnly} />
              </section>
            );
          })}
        </div>
      )}
    </div>
  );
}
