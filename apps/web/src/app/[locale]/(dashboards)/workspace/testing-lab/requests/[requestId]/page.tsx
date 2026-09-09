import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingRequestPage({
  params,
}: {
  params: Promise<{ locale: string; requestId: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/events", locale });
}
