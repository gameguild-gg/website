import { TestingEventLearningDialog } from '@/components/testing-lab/testing-event-management';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { getCourses } from '@/lib/courses/services/course.service';
import { getCourseContent } from '@/lib/learning/queries/course';
import { isTestingEventReadOnly } from '@/lib/testing-lab/event-workspace';
import { getTestingEventWorkspaceData } from '@/lib/testing-lab/events-queries';
import { formatTestingEventStatus } from '@/lib/testing-lab/format';
import { Badge } from '@game-guild/ui/components/badge';
import { BookOpenCheck } from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingEventLearningPage({
  params,
}: {
  params: Promise<{ eventId: string }>;
}) {
  const { eventId } = await params;
  const [detail, courses] = await Promise.all([
    getTestingEventWorkspaceData(eventId),
    getCourses(),
  ]);
  if (!detail.event) notFound();

  const courseContent = await Promise.all(
    courses
      .filter((course) => course.id)
      .slice(0, 50)
      .map(async (course) => ({
        courseId: String(course.id),
        courseTitle: course.title ?? course.slug ?? 'Untitled course',
        content: await getCourseContent(String(course.id)),
      })),
  );
  const activities = courseContent.flatMap((course) =>
    course.content.items.map((item) => ({
      id: item.id,
      courseId: course.courseId,
      label: `${course.courseTitle} / ${item.title || item.type}`,
    })),
  );
  const event = detail.event;

  return (
    <div className="space-y-5">
      <TestingLabPageHeader
        headingLevel={2}
        icon={BookOpenCheck}
        title="Learning evidence"
        description="Connect attendance and feedback evidence to one course activity without moving grading ownership into Testing Lab."
        actions={
          <TestingEventLearningDialog
            event={event}
            activities={activities}
            readOnly={isTestingEventReadOnly(event)}
          />
        }
      />

      <section className="rounded-md border p-4">
        <h2 className="font-semibold">Current connection</h2>
        {event.courseId ? (
          <dl className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Course</dt>
              <dd className="mt-1 text-sm">{event.courseId}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Activity</dt>
              <dd className="mt-1 text-sm">{event.learningActivityId ?? 'Not selected'}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Cohort</dt>
              <dd className="mt-1 text-sm">{event.cohortId ?? 'All eligible learners'}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Completion requirement</dt>
              <dd className="mt-1">
                <Badge variant="outline">
                  {formatTestingEventStatus(event.learningCompletionRequirement)}
                </Badge>
              </dd>
            </div>
          </dl>
        ) : (
          <div className="mt-4 rounded-md border border-dashed p-6 text-center">
            <p className="font-medium">No learning activity connected</p>
            <p className="mt-1 text-sm text-muted-foreground">
              Attendance and feedback remain available as standalone Testing Lab evidence.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
