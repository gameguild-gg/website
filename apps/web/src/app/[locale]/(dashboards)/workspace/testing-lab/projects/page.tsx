import { redirect } from "@/i18n/navigation";

export default async function LegacyTestingProjectsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  redirect({ href: "/workspace/testing-lab/events", locale });
}
