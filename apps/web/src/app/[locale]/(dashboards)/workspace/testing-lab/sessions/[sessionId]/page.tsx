import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingSessionPage({
  params,
}: {
  params: Promise<{ locale: string; sessionId: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/events", locale });
}
