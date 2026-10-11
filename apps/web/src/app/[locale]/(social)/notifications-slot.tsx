import { AppShellHeaderMenu } from '@/components/app/app-shell-header-menu';
import type { WorkspaceUser } from '@/components/console/workspace-user-menu';
import {
  getDashboardNotificationSummary,
  type DashboardNotificationSummary,
} from '@/lib/dashboard-notifications';

const EMPTY_SUMMARY: DashboardNotificationSummary = { items: [], unreadCount: 0 };

interface NotificationsSlotProps {
  userId: string;
  user: WorkspaceUser;
}

/**
 * Suspense slot for the social layout: fetches the notifications summary off
 * the critical path, degrading to an empty summary on failure.
 */
export async function NotificationsSlot({ userId, user }: NotificationsSlotProps): Promise<React.JSX.Element> {
  let notifications: DashboardNotificationSummary;
  try {
    notifications = await getDashboardNotificationSummary(userId);
  } catch {
    notifications = EMPTY_SUMMARY;
  }

  return <AppShellHeaderMenu notifications={notifications} user={user} />;
}
