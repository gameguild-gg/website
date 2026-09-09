import { redirect } from 'next/navigation';

export default async function TestingEventPage({
  params,
}: {
  params: Promise<{ eventId: string }>;
}) {
  const { eventId } = await params;
  redirect(`/workspace/testing-lab/events/${eventId}/overview`);
}
