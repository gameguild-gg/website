import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingPeoplePage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/events", locale });
}
