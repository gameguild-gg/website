import { requireDashboardCapability } from "@/lib/require-dashboard-capability";
import type { ReactNode } from "react";

export default async function TestingLabAccessSettingsLayout({
  children,
}: {
  children: ReactNode;
}) {
  await requireDashboardCapability("TestingLab.ManageSettings");
  return children;
}
