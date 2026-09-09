import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingProjectPage({
  params,
}: {
  params: Promise<{ locale: string; projectId: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/events", locale });
}
