import { forbidden } from 'next/navigation';
import { getDashboardContexts, hasAnyDashboardCapability, type DashboardContexts } from './dashboard-contexts';

export async function requireAnyDashboardCapability(...capabilities: string[]): Promise<DashboardContexts> {
  const contexts = await getDashboardContexts();
  if (!hasAnyDashboardCapability(contexts.capabilities, ...capabilities)) forbidden();
  return contexts;
}

export async function requireDashboardCapability(capability: string): Promise<void> {
  await requireAnyDashboardCapability(capability);
}
