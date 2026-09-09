import { requireDashboardCapability } from '@/lib/require-dashboard-capability';
import type { ReactNode } from 'react';

export default async function ApplicationsManagementLayout({ children }: { children: ReactNode }) {
  await requireDashboardCapability('TestingLab.ReviewApplications');
  return children;
}
