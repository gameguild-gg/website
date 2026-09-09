import { requireDashboardCapability } from '@/lib/require-dashboard-capability';
import type { ReactNode } from 'react';

export default async function AnalyticsManagementLayout({ children }: { children: ReactNode }) {
  await requireDashboardCapability('TestingLab.ViewAnalytics');
  return children;
}
