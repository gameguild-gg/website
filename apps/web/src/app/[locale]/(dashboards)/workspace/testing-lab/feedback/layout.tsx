import { requireDashboardCapability } from '@/lib/require-dashboard-capability';
import type { ReactNode } from 'react';

export default async function FeedbackManagementLayout({ children }: { children: ReactNode }) {
  await requireDashboardCapability('TestingLab.ManageFeedback');
  return children;
}
