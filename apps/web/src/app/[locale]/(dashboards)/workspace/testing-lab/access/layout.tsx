import { requireDashboardCapability } from '@/lib/require-dashboard-capability';
import type { ReactNode } from 'react';

export default async function AccessManagementLayout({ children }: { children: ReactNode }) {
  await requireDashboardCapability('TestingLab.ManageSettings');
  return children;
}
