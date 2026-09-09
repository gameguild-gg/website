import { requireAnyDashboardCapability } from '@/lib/require-dashboard-capability';
import { TestingLabSettingsNav } from '@/components/testing-lab/testing-lab-settings-nav';
import type { ReactNode } from 'react';

export default async function TestingSettingsManagementLayout({ children }: { children: ReactNode }) {
  const { capabilities } = await requireAnyDashboardCapability(
    'TestingLab.ManageSettings',
    'TestingLab.ViewAnalytics',
  );
  return <div className="grid min-w-0 lg:grid-cols-[13rem_minmax(0,1fr)]">
    <aside className="border-b p-3 lg:min-h-[calc(100dvh-4rem)] lg:border-b-0 lg:border-r lg:p-4">
      <TestingLabSettingsNav capabilities={capabilities} />
    </aside>
    <div className="min-w-0">{children}</div>
  </div>;
}
