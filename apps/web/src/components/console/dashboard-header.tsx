'use client';

import { ThemeToggle } from '@/components/ui/theme-toggle';
import { Link, usePathname } from '@/i18n/navigation';
import { DashboardUserMenu, type DashboardUser } from './dashboard-user-menu';
import { Badge } from '@game-guild/ui/components/badge';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@game-guild/ui/components/breadcrumb';
import { Button } from '@game-guild/ui/components/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@game-guild/ui/components/dropdown-menu';
import { Separator } from '@game-guild/ui/components/separator';
import { SidebarTrigger } from '@game-guild/ui/components/sidebar';
import { Bell, CheckCheck, Command, House, Mail, Search } from 'lucide-react';
import * as React from 'react';
import { toast } from 'sonner';
import type { DashboardNotificationItem, DashboardNotificationSummary } from '@/lib/dashboard-notifications';
import {
  markAllNotificationsReadAction,
  setNotificationReadAction,
  type NotificationReadActionResult,
} from '@/lib/notifications/mark-read-action';
import { openDashboardCommandPalette } from './dashboard-command-palette';

const COURSE_ROUTE_PREFIX = ['dashboard', 'learning', 'courses'];

interface NotificationItemProps {
  item: DashboardNotificationItem;
  onSetRead: (item: DashboardNotificationItem, isRead: boolean) => void;
}

function NotificationMenuItem({ item, onSetRead }: NotificationItemProps) {
  const toggleOnActivate = () => {
    // Read items with a link navigate only; toggling them back to unread on
    // every visit would fight the link's purpose.
    if (item.isRead && item.actionUrl?.startsWith('/')) return;
    onSetRead(item, !item.isRead);
  };

  const content = (
    <div className="flex w-full flex-col gap-1">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-medium">{item.title}</p>
        {item.isRead ? (
          <button
            type="button"
            aria-label={`Mark ${item.title} unread`}
            title="Mark unread"
            className="mt-0.5 shrink-0 rounded-sm p-0.5 text-muted-foreground opacity-0 transition-opacity hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100"
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              onSetRead(item, false);
            }}
          >
            <Mail className="size-3.5" />
          </button>
        ) : (
          <span className="mt-1 size-2 rounded-full bg-primary" aria-label="Unread" />
        )}
      </div>
      <p className="line-clamp-2 text-xs text-muted-foreground">{item.message}</p>
      <p className="text-xs text-muted-foreground">{item.createdLabel}</p>
      {item.actionText && <p className="text-xs font-medium text-primary">{item.actionText}</p>}
    </div>
  );

  if (item.actionUrl?.startsWith('/')) {
    return (
      <DropdownMenuItem asChild className="group">
        <Link href={item.actionUrl} onClick={toggleOnActivate}>
          {content}
        </Link>
      </DropdownMenuItem>
    );
  }

  return (
    <DropdownMenuItem className="group" onClick={toggleOnActivate}>
      {content}
    </DropdownMenuItem>
  );
}

interface DashboardHeaderProps {
  notifications?: DashboardNotificationSummary;
  user: DashboardUser;
}

export function DashboardHeader({ notifications, user }: DashboardHeaderProps) {
  const pathname = usePathname();
  const isWorkspace = pathname?.startsWith('/workspace') ?? false;
  const notificationSummary = notifications ?? { items: [], unreadCount: 0 };
  const [readOverrides, setReadOverrides] = React.useState<Record<string, boolean>>({});
  const [hiddenUnreadCount, setHiddenUnreadCount] = React.useState<number | null>(null);

  const items = notificationSummary.items.map((item) =>
    readOverrides[item.id] === undefined ? item : { ...item, isRead: readOverrides[item.id] },
  );
  const shownUnreadCount = items.filter((item) => !item.isRead).length;
  const serverShownUnreadCount = notificationSummary.items.filter((item) => !item.isRead).length;
  const derivedHiddenUnreadCount = Math.max(
    0,
    notificationSummary.unreadCount - serverShownUnreadCount,
  );
  const unreadCount =
    shownUnreadCount + (hiddenUnreadCount ?? derivedHiddenUnreadCount);
  const unreadLabel = unreadCount > 99 ? '99+' : String(unreadCount);

  const applyReadChanges = async (
    changes: Array<{ id: string; isRead: boolean }>,
    invoke: () => Promise<NotificationReadActionResult>,
    markAll: boolean,
  ) => {
    const previousOverrides = readOverrides;
    const previousHiddenUnreadCount = hiddenUnreadCount;
    setReadOverrides((current) => {
      const next = { ...current };
      for (const change of changes) {
        next[change.id] = change.isRead;
      }
      return next;
    });
    if (markAll) {
      setHiddenUnreadCount(0);
    }

    const result = await invoke();
    if (!result.success) {
      setReadOverrides(previousOverrides);
      setHiddenUnreadCount(previousHiddenUnreadCount);
      toast.error('Failed to update notifications. Please try again.');
    }
  };

  const handleSetRead = (item: DashboardNotificationItem, isRead: boolean) => {
    if (item.isRead === isRead) return;
    void applyReadChanges([{ id: item.id, isRead }], () => setNotificationReadAction(item.id, isRead), false);
  };

  const handleMarkAllRead = () => {
    if (shownUnreadCount === 0) return;
    const changes = items.filter((item) => !item.isRead).map((item) => ({ id: item.id, isRead: true }));
    void applyReadChanges(changes, markAllNotificationsReadAction, true);
  };

  // Generate breadcrumbs from pathname
  const generateBreadcrumbs = () => {
    if (!pathname) return [];

    const paths = pathname
      .split('/')
      .filter(Boolean)
      .filter((segment) => !/^[a-z]{2}(?:-[A-Z]{2})?$/.test(segment));

    // If we're at the root, don't show anything
    if (paths.length === 0) {
      return [];
    }

    const breadcrumbs: Array<{ label: string; href?: string }> = [];

    let currentPath = '';
    paths.forEach((path, index) => {
      currentPath += `/${path}`;
      const label = path
        .replace(/-/g, ' ')
        .replace(/\b\w/g, (char) => char.toUpperCase());

      // Last item shouldn't have href (it's the current page)
      if (index === paths.length - 1) {
        breadcrumbs.push({ label, href: undefined });
      } else {
        breadcrumbs.push({ label, href: currentPath });
      }
    });

    if (paths.length >= 5 && COURSE_ROUTE_PREFIX.every((segment, index) => paths[index] === segment)) {
      return breadcrumbs.slice(0, 3).concat(breadcrumbs.slice(-2));
    }

    return breadcrumbs.slice(-5);
  };

  const breadcrumbs = generateBreadcrumbs();

  return (
    <header className="sticky top-0 z-40 grid h-16 min-w-0 shrink-0 grid-cols-[minmax(0,1fr)_auto] items-center gap-2 border-b px-3 sm:px-4 xl:grid-cols-[minmax(0,1fr)_minmax(16rem,32rem)_minmax(0,1fr)]">
      <div className="flex min-w-0 items-center gap-2">
        {isWorkspace ? (
          <SidebarTrigger className="md:hidden" />
        ) : (
          <SidebarTrigger />
        )}
        {breadcrumbs.length > 0 && (
          <>
            {!isWorkspace && (
              <Separator orientation="vertical" className="mr-2 hidden data-[orientation=vertical]:h-4 sm:block" />
            )}
            <Breadcrumb aria-label="Dashboard breadcrumb" className="hidden min-w-0 flex-1 overflow-hidden sm:block">
              <BreadcrumbList className="flex-nowrap overflow-hidden">
                <BreadcrumbItem>
                  {breadcrumbs[0]?.href ? (
                    <BreadcrumbLink asChild>
                      <Link href={breadcrumbs[0].href}>{breadcrumbs[0].label}</Link>
                    </BreadcrumbLink>
                  ) : (
                    <BreadcrumbPage>{breadcrumbs[0]?.label}</BreadcrumbPage>
                  )}
                </BreadcrumbItem>
                {breadcrumbs.length > 1 && <BreadcrumbSeparator />}
                {breadcrumbs.slice(1).map((item) => (
                  <React.Fragment key={`${item.href ?? 'current'}:${item.label}`}>
                    <BreadcrumbItem>
                      {item.href ? (
                        <BreadcrumbLink asChild className="max-w-24 truncate md:max-w-40 xl:max-w-64">
                          <Link href={item.href}>{item.label}</Link>
                        </BreadcrumbLink>
                      ) : (
                        <BreadcrumbPage className="max-w-24 truncate md:max-w-40 xl:max-w-64">{item.label}</BreadcrumbPage>
                      )}
                    </BreadcrumbItem>
                    {item.href && <BreadcrumbSeparator />}
                  </React.Fragment>
                ))}
              </BreadcrumbList>
            </Breadcrumb>
          </>
        )}
      </div>
      <div className="hidden min-w-0 items-center justify-center xl:flex">
        {/* Search */}
        <button
          type="button"
          onClick={openDashboardCommandPalette}
          className="relative flex h-10 w-full max-w-sm items-center rounded-md border bg-background px-3 text-left text-sm text-muted-foreground transition-colors hover:bg-muted/50 lg:max-w-md"
          aria-label="Search dashboard"
        >
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <span className="min-w-0 flex-1 truncate pl-7 pr-16">Search dashboard...</span>
          <kbd className="pointer-events-none absolute right-3 top-1/2 hidden h-5 -translate-y-1/2 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 sm:flex">
            <Command className="size-3" />
          </kbd>
        </button>
      </div>
      <div
        role="group"
        aria-label="Dashboard actions"
        className="flex shrink-0 items-center justify-end gap-1 sm:gap-2"
      >
        {isWorkspace && (
          <Button
            asChild
            variant="ghost"
            size="sm"
            className="gap-1.5 px-2 text-muted-foreground hover:text-foreground"
          >
            <Link href="/" aria-label="Open Community feed" title="Open Community feed">
              <House className="size-4" aria-hidden="true" />
              <span className="hidden md:inline">Feed</span>
            </Link>
          </Button>
        )}

        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="xl:hidden"
          onClick={openDashboardCommandPalette}
          aria-label="Search dashboard"
        >
          <Search className="size-5" />
        </Button>

        {/* Theme Toggle */}
        <ThemeToggle />

        {/* Notifications */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="relative">
              <Bell className="size-5" />
              {unreadCount > 0 && (
                <Badge variant="destructive" className="absolute -right-1 -top-1 h-5 min-w-5 rounded-full px-1 text-xs">
                  {unreadLabel}
                </Badge>
              )}
              <span className="sr-only">Notifications</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-80">
            <DropdownMenuLabel>Notifications</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <div className="max-h-[300px] overflow-y-auto">
              {notificationSummary.items.length > 0 ? (
                items.map((item) => (
                  <NotificationMenuItem key={item.id} item={item} onSetRead={handleSetRead} />
                ))
              ) : (
                <div className="px-3 py-6 text-center">
                  <p className="text-sm font-medium">No notifications</p>
                  <p className="mt-1 text-xs text-muted-foreground">New account updates will appear here.</p>
                </div>
              )}
            </div>
            <DropdownMenuSeparator />
            <div className="flex items-center justify-between gap-2 px-3 py-2">
              <p className="text-xs font-normal text-muted-foreground">Showing latest account notifications</p>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 gap-1 px-2 text-xs"
                disabled={shownUnreadCount === 0}
                onClick={handleMarkAllRead}
              >
                <CheckCheck className="size-3.5" />
                Mark all read
              </Button>
            </div>
          </DropdownMenuContent>
        </DropdownMenu>

        <DashboardUserMenu user={user} />
      </div>
    </header>
  );
}
