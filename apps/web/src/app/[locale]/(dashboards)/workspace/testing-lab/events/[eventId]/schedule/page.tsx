import {
  CreateTestingEventSlotDialog,
  ManageTestingEventSlotDialog,
} from '@/components/testing-lab/testing-event-management';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import {
  formatCapacity,
  formatEventDateTime,
  isTestingEventReadOnly,
} from '@/lib/testing-lab/event-workspace';
import { getTestingEventWorkspaceData } from '@/lib/testing-lab/events-queries';
import { Badge } from '@game-guild/ui/components/badge';
import { CalendarDays, MapPin } from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingEventSchedulePage({
  params,
}: {
  params: Promise<{ eventId: string }>;
}) {
  const { eventId } = await params;
  const detail = await getTestingEventWorkspaceData(eventId);
  if (!detail.event) notFound();
  const readOnly = isTestingEventReadOnly(detail.event);

  return (
    <div className="space-y-5">
      <TestingLabPageHeader
        headingLevel={2}
        icon={CalendarDays}
        title="Schedule and capacity"
        description="Each slot owns its time, location, tester limit, and approved-project capacity."
        actions={!readOnly ? <CreateTestingEventSlotDialog eventId={eventId} /> : undefined}
      />

      {detail.slots.length === 0 ? (
        <div className="rounded-md border border-dashed p-8 text-center">
          <h2 className="font-medium">No testing slots scheduled</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Add the first online or in-person window to make capacity available.
          </p>
        </div>
      ) : (
        <div className="grid gap-3 xl:grid-cols-2">
          {detail.slots.map((slot) => (
            <article key={slot.id} className="rounded-md border p-4">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold">{formatEventDateTime(slot.startsAt)}</h2>
                    <Badge variant="outline">{slot.mode}</Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Ends {formatEventDateTime(slot.endsAt)}
                  </p>
                  <p className="mt-3 flex items-start gap-2 text-sm">
                    <MapPin className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                    <span>
                      {slot.mode === 'Online'
                        ? slot.meetingUrl ?? 'Online link not configured'
                        : `${slot.campusName ?? 'Campus not set'} / ${slot.roomName ?? 'Room not set'}`}
                    </span>
                  </p>
                </div>
                {!readOnly ? <ManageTestingEventSlotDialog eventId={eventId} slot={slot} /> : null}
              </div>
              <dl className="mt-4 grid grid-cols-2 gap-3 border-t pt-4">
                <div>
                  <dt className="text-xs text-muted-foreground">Testers</dt>
                  <dd className="mt-1 font-medium">
                    {formatCapacity(slot.registeredTesterCount, slot.maxTesters)}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">Projects</dt>
                  <dd className="mt-1 font-medium">
                    {formatCapacity(slot.approvedProjectCount, slot.maxProjects)}
                  </dd>
                </div>
              </dl>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
