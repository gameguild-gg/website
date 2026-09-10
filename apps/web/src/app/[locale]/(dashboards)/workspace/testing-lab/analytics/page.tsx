import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingAnalyticsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/settings/analytics", locale });
}
