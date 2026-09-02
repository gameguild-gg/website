'use client';

import { Link, usePathname } from '@/i18n/navigation';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@game-guild/ui/components/collapsible';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarRail,
  useSidebar,
} from '@game-guild/ui/components/sidebar';
import {
  Accessibility,
  BarChart3,
  BookOpen,
  ChevronRight,
  ClipboardList,
  CircleDollarSign,
  FileText,
  FlaskConical,
  FolderOpen,
  HeadphonesIcon,
  LayoutDashboard,
  MapPin,
  FolderKanban,
  Globe2,
  MailCheck,
  Palette,
  PanelLeftClose,
  PanelLeftOpen,
  MessageSquareText,
  Rocket,
  Settings,
  ShieldCheck,
  UserCog,
  User,
  Users,
  type LucideIcon,
} from 'lucide-react';
import * as React from 'react';
import type { DashboardContextSummary } from '@/lib/dashboard-contexts';
import { GraduationCap } from 'lucide-react';
import { ContextSwitcher } from './team-switcher';
import { TenantSwitcher, type Tenant } from './tenant-switcher';

// Types for navigation structure
export interface DashboardNavSubItem {
  title: string;
  url: string;
  icon: LucideIcon;
  isActive?: boolean;
  badge?: string;
  requiredCapabilities?: readonly string[];
}

export interface DashboardNavItem {
  title: string;
  url?: string;
  icon?: LucideIcon;
  items: DashboardNavSubItem[];
  requiredCapabilities?: readonly string[];
}

export interface DashboardNavGroupItem {
  title: string;
  url?: string;
  icon?: LucideIcon;
  items?: DashboardNavSubItem[];
  subGroups?: DashboardNavItem[];
  requiredCapabilities?: readonly string[];
}

export interface DashboardNavGroup {
  label: string;
  items: DashboardNavGroupItem[];
}

// Game Guild Dashboard navigation structure
// Routes map to: /[locale]/(dashboard)/dashboard/...
export const dashboardNavigationData: DashboardNavGroup[] = [
  {
    label: 'Workspace',
    items: [
      {
        title: 'Home',
        url: '/workspace',
        icon: LayoutDashboard,
      },
      {
        title: 'Projects',
        url: '/workspace/projects',
        icon: FolderKanban,
      },
      {
        title: 'Teams',
        url: '/workspace/teams',
        icon: Users,
      },
      {
        title: 'Learning',
        icon: BookOpen,
        subGroups: [
          { title: 'Overview', url: '/workspace/learning', icon: LayoutDashboard, items: [] },
          { title: 'Courses', url: '/workspace/learning/courses', icon: BookOpen, items: [] },
          { title: 'Tutorials', url: '/workspace/learning/tutorials', icon: FileText, items: [] },
          { title: 'Resources', url: '/workspace/learning/resources', icon: FolderOpen, items: [] },
        ],
      },
      {
        title: 'Invitations',
        url: '/workspace/invitations',
        icon: MailCheck,
      },
      {
        title: 'Settings',
        icon: Settings,
        subGroups: [
          { title: 'Profile', url: '/workspace/settings/profile', icon: User, items: [] },
          { title: 'Account', url: '/workspace/settings/account', icon: UserCog, items: [] },
          { title: 'Appearance', url: '/workspace/settings/appearance', icon: Palette, items: [] },
          { title: 'Localization', url: '/workspace/settings/localization', icon: Globe2, items: [] },
          { title: 'Privacy', url: '/workspace/settings/privacy', icon: ShieldCheck, items: [] },
          { title: 'Accessibility', url: '/workspace/settings/accessibility', icon: Accessibility, items: [] },
        ],
      },
    ],
  },
  {
    label: 'Community administration',
    items: [
      {
        title: 'Overview',
        url: '/console/community',
        icon: LayoutDashboard,
        requiredCapabilities: ['Community.Manage'],
      },
      {
        title: 'Members',
        icon: Users,
        requiredCapabilities: ['Community.ManageMembers'],
        subGroups: [
          {
            title: 'Overview',
            url: '/console/community/members',
            icon: LayoutDashboard,
            items: [],
            requiredCapabilities: ['Community.ManageMembers'],
          },
          {
            title: 'Users',
            url: '/console/community/members/users',
            icon: UserCog,
            items: [],
            requiredCapabilities: ['Community.ManageMembers'],
          },
          {
            title: 'Groups',
            url: '/console/community/members/groups',
            icon: Users,
            items: [],
            requiredCapabilities: ['Community.ManageMembers'],
          },
          {
            title: 'Support',
            url: '/console/community/members/support',
            icon: HeadphonesIcon,
            items: [],
            requiredCapabilities: ['Community.ManageSupport'],
          },
        ],
      },
      {
        title: 'Teams',
        url: '/console/community/teams',
        icon: Users,
        requiredCapabilities: ['Community.ManageTeams'],
      },
      {
        title: 'Projects',
        url: '/console/community/projects',
        icon: FolderKanban,
        requiredCapabilities: ['Community.ManageProjects'],
      },
      {
        title: 'Testing Lab',
        icon: FlaskConical,
        requiredCapabilities: [
          'TestingLab.ManageEvents',
          'TestingLab.ReviewApplications',
          'TestingLab.ManageParticipants',
          'TestingLab.ManageFeedback',
          'TestingLab.ViewAnalytics',
          'TestingLab.ManageSettings',
        ],
        subGroups: [
          {
            title: 'Overview',
            url: '/console/community/testing-lab',
            icon: LayoutDashboard,
            items: [],
            requiredCapabilities: [
              'TestingLab.ManageEvents',
              'TestingLab.ReviewApplications',
              'TestingLab.ManageParticipants',
              'TestingLab.ManageFeedback',
              'TestingLab.ViewAnalytics',
              'TestingLab.ManageSettings',
            ],
          },
          {
            title: 'Events',
            url: '/console/community/testing-lab/events',
            icon: FlaskConical,
            items: [],
            requiredCapabilities: ['TestingLab.ManageEvents'],
          },
          {
            title: 'Applications',
            url: '/console/community/testing-lab/applications',
            icon: ClipboardList,
            items: [],
            requiredCapabilities: ['TestingLab.ReviewApplications'],
          },
          {
            title: 'Projects',
            url: '/console/community/testing-lab/projects',
            icon: FolderKanban,
            items: [],
            requiredCapabilities: ['TestingLab.ReviewApplications'],
          },
          {
            title: 'Participants',
            url: '/console/community/testing-lab/participants',
            icon: Users,
            items: [],
            requiredCapabilities: ['TestingLab.ManageParticipants'],
          },
          {
            title: 'Feedback',
            url: '/console/community/testing-lab/feedback',
            icon: MessageSquareText,
            items: [],
            requiredCapabilities: ['TestingLab.ManageFeedback'],
          },
          {
            title: 'Analytics',
            url: '/console/community/testing-lab/analytics',
            icon: BarChart3,
            items: [],
            requiredCapabilities: ['TestingLab.ViewAnalytics'],
          },
          {
            title: 'Locations',
            url: '/console/community/testing-lab/locations',
            icon: MapPin,
            items: [],
            requiredCapabilities: ['TestingLab.ManageSettings'],
          },
          {
            title: 'Access',
            url: '/console/community/testing-lab/access',
            icon: ShieldCheck,
            items: [],
            requiredCapabilities: ['TestingLab.ManageSettings'],
          },
          {
            title: 'Settings',
            url: '/console/community/testing-lab/settings',
            icon: Settings,
            items: [],
            requiredCapabilities: ['TestingLab.ManageSettings'],
          },
        ],
      },
      {
        title: 'Launch Pad',
        url: '/console/community/launch-pad',
        icon: Rocket,
        requiredCapabilities: [
          'LaunchPad.ManageEvents',
          'LaunchPad.ReviewApplications',
          'LaunchPad.ManageParticipants',
          'LaunchPad.ViewAnalytics',
          'LaunchPad.ManageSettings',
        ],
        subGroups: [
          {
            title: 'Overview',
            url: '/console/community/launch-pad',
            icon: LayoutDashboard,
            items: [],
            requiredCapabilities: [
              'LaunchPad.ManageEvents',
              'LaunchPad.ReviewApplications',
              'LaunchPad.ManageParticipants',
              'LaunchPad.ViewAnalytics',
              'LaunchPad.ManageSettings',
            ],
          },
          {
            title: 'Events',
            url: '/console/community/launch-pad/events',
            icon: Rocket,
            items: [],
            requiredCapabilities: ['LaunchPad.ManageEvents'],
          },
          {
            title: 'Applications',
            url: '/console/community/launch-pad/applications',
            icon: ClipboardList,
            items: [],
            requiredCapabilities: ['LaunchPad.ReviewApplications'],
          },
          {
            title: 'Participants',
            url: '/console/community/launch-pad/participants',
            icon: Users,
            items: [],
            requiredCapabilities: ['LaunchPad.ManageParticipants'],
          },
          {
            title: 'Analytics',
            url: '/console/community/launch-pad/analytics',
            icon: BarChart3,
            items: [],
            requiredCapabilities: ['LaunchPad.ViewAnalytics'],
          },
          {
            title: 'Settings',
            url: '/console/community/launch-pad/settings',
            icon: Settings,
            items: [],
            requiredCapabilities: ['LaunchPad.ManageSettings'],
          },
        ],
      },
      {
        title: 'Learning',
        icon: BookOpen,
        requiredCapabilities: ['Learning.Manage'],
        subGroups: [
          {
            title: 'Overview',
            url: '/console/learning',
            icon: LayoutDashboard,
            items: [],
            requiredCapabilities: ['Learning.Manage'],
          },
          {
            title: 'Courses',
            url: '/console/learning/courses',
            icon: BookOpen,
            items: [],
            requiredCapabilities: ['Learning.Manage'],
          },
          {
            title: 'Tutorials',
            url: '/console/learning/tutorials',
            icon: FileText,
            items: [],
            requiredCapabilities: ['Learning.Manage'],
          },
          {
            title: 'Resources',
            url: '/console/learning/resources',
            icon: FolderOpen,
            items: [],
            requiredCapabilities: ['Learning.Manage'],
          },
        ],
      },
    ],
  },
  {
    label: 'Platform Management',
    items: [
      {
        title: 'Economy',
        icon: CircleDollarSign,
        requiredCapabilities: ['Economy.ManagePayouts'],
        subGroups: [
          {
            title: 'Payout review',
            url: '/console/economy/payout-reviews',
            icon: ClipboardList,
            items: [],
            requiredCapabilities: ['Economy.ManagePayouts'],
          },
        ],
      },
      {
        title: 'Roles',
        url: '/console/platform/roles',
        icon: ShieldCheck,
        requiredCapabilities: ['Platform.ManageRoles'],
      },
    ],
  },
];

function hasAnyCapability(
  requiredCapabilities: readonly string[] | undefined,
  capabilities: ReadonlySet<string>,
): boolean {
  return (
    !requiredCapabilities?.length ||
    requiredCapabilities.some((capability) => capabilities.has(capability))
  );
}

export function filterDashboardNavigation(
  groups: DashboardNavGroup[],
  actorCapabilities: readonly string[],
): DashboardNavGroup[] {
  const capabilities = new Set(actorCapabilities);

  return groups.flatMap((group) => {
    const items = group.items.flatMap((item) => {
      if (!hasAnyCapability(item.requiredCapabilities, capabilities)) return [];

      const nestedItems = item.items?.filter((nested) =>
        hasAnyCapability(nested.requiredCapabilities, capabilities),
      );
      const subGroups = item.subGroups?.filter((nested) =>
        hasAnyCapability(nested.requiredCapabilities, capabilities),
      );

      if (item.items?.length && !nestedItems?.length) return [];
      if (item.subGroups?.length && !subGroups?.length) return [];

      return [{ ...item, items: nestedItems, subGroups }];
    });

    return items.length ? [{ ...group, items }] : [];
  });
}

export function flattenDashboardNavigationItems(groups: DashboardNavGroup[] = dashboardNavigationData): DashboardNavSubItem[] {
  const items: DashboardNavSubItem[] = [];

  for (const group of groups) {
    for (const item of group.items) {
      if (item.url && item.icon) {
        items.push({ title: item.title, url: item.url, icon: item.icon });
      }

      if (item.items?.length) {
        items.push(...item.items);
      }

      if (item.subGroups?.length) {
        for (const subGroup of item.subGroups) {
          if (subGroup.url && subGroup.icon) {
            items.push({ title: subGroup.title, url: subGroup.url, icon: subGroup.icon });
          }

          items.push(...subGroup.items);
        }
      }
    }
  }

  return items;
}

function NavGroups({ groups }: { groups: DashboardNavGroup[] }) {
  const pathname = usePathname();
  const [openItems, setOpenItems] = React.useState<Set<string>>(new Set());

  const toggleItem = (key: string) => {
    setOpenItems((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  };

  return (
    <>
      {groups.map((group) => (
        <SidebarGroup key={group.label}>
          <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {group.items.map((item) => {
                const Icon = item.icon;
                const hasItems = item.items && item.items.length > 0;
                const hasSubGroups = item.subGroups && item.subGroups.length > 0;
                const isOpen = openItems.has(item.title);

                // Simple link item (no children)
                if (!hasItems && !hasSubGroups && item.url) {
                  const isActive = pathname === item.url || pathname?.endsWith(item.url);
                  return (
                    <SidebarMenuItem key={item.title}>
                      <SidebarMenuButton asChild isActive={isActive}>
                        <Link href={item.url}>
                          {Icon && <Icon className="size-4" />}
                          <span>{item.title}</span>
                        </Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                }

                // Collapsible item with sub-items
                if (hasItems) {
                  return (
                    <Collapsible key={item.title} open={isOpen} onOpenChange={() => toggleItem(item.title)} className="group/collapsible">
                      <SidebarMenuItem>
                        <CollapsibleTrigger asChild>
                          <SidebarMenuButton>
                            {Icon && <Icon className="size-4" />}
                            <span>{item.title}</span>
                            <ChevronRight className="ml-auto size-4 transition-transform group-data-[state=open]/collapsible:rotate-90" />
                          </SidebarMenuButton>
                        </CollapsibleTrigger>
                        <CollapsibleContent>
                          <SidebarMenuSub>
                            {item.items!.map((subItem) => {
                              const isActive = pathname === subItem.url || pathname?.endsWith(subItem.url);
                              return (
                                <SidebarMenuSubItem key={subItem.title}>
                                  <SidebarMenuSubButton asChild isActive={isActive}>
                                    <Link href={subItem.url}>
                                      <subItem.icon className="size-4" />
                                      <span>{subItem.title}</span>
                                      {subItem.badge && (
                                        <span className="ml-auto rounded-full bg-primary px-2 py-0.5 text-xs text-primary-foreground">{subItem.badge}</span>
                                      )}
                                    </Link>
                                  </SidebarMenuSubButton>
                                </SidebarMenuSubItem>
                              );
                            })}
                          </SidebarMenuSub>
                        </CollapsibleContent>
                      </SidebarMenuItem>
                    </Collapsible>
                  );
                }

                // Collapsible item with sub-groups (nested)
                if (hasSubGroups) {
                  return (
                    <Collapsible key={item.title} open={isOpen} onOpenChange={() => toggleItem(item.title)} className="group/collapsible">
                      <SidebarMenuItem>
                        <CollapsibleTrigger asChild>
                          <SidebarMenuButton>
                            {Icon && <Icon className="size-4" />}
                            <span>{item.title}</span>
                            <ChevronRight className="ml-auto size-4 transition-transform group-data-[state=open]/collapsible:rotate-90" />
                          </SidebarMenuButton>
                        </CollapsibleTrigger>
                        <CollapsibleContent>
                          <SidebarMenuSub>
                            {item.subGroups!.map((subGroup) => {
                              const isActive = pathname === subGroup.url || pathname?.endsWith(subGroup.url ?? '');
                              const SubIcon = subGroup.icon;
                              return (
                                <SidebarMenuSubItem key={subGroup.title}>
                                  <SidebarMenuSubButton asChild isActive={isActive}>
                                    <Link href={subGroup.url || '#'}>
                                      {SubIcon && <SubIcon className="size-4" />}
                                      <span>{subGroup.title}</span>
                                    </Link>
                                  </SidebarMenuSubButton>
                                </SidebarMenuSubItem>
                              );
                            })}
                          </SidebarMenuSub>
                        </CollapsibleContent>
                      </SidebarMenuItem>
                    </Collapsible>
                  );
                }

                return null;
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      ))}
    </>
  );
}

interface DashboardSidebarProps extends React.ComponentProps<typeof Sidebar> {
  navigation?: DashboardNavGroup[];
  contexts?: readonly DashboardContextSummary[];
}

/** Default console tenant — GameGuild platform until multi-tenant switching ships. */
const consoleTenants: Tenant[] = [
  { id: 'gameguild', name: 'GameGuild', logo: GraduationCap, plan: 'Platform' },
];

export function DashboardSidebarFooter() {
  const pathname = usePathname();
  const { isMobile, openMobile, state, toggleSidebar } = useSidebar();
  const isWorkspace = pathname?.startsWith('/workspace') ?? false;

  if (!isWorkspace) {
    return <SidebarFooter />;
  }

  const expanded = isMobile ? openMobile : state === 'expanded';
  const label = isMobile
    ? expanded
      ? 'Close sidebar'
      : 'Open sidebar'
    : expanded
      ? 'Collapse sidebar'
      : 'Expand sidebar';
  const Icon = expanded ? PanelLeftClose : PanelLeftOpen;

  return (
    <SidebarFooter className="items-center p-2">
      <button
        type="button"
        onClick={toggleSidebar}
        aria-label={label}
        title={label}
        className="flex size-8 items-center justify-center rounded-md text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring"
      >
        <Icon className="size-4" aria-hidden="true" />
      </button>
    </SidebarFooter>
  );
}

export function DashboardSidebar({
  navigation = filterDashboardNavigation(dashboardNavigationData, []),
  contexts = [],
  ...props
}: DashboardSidebarProps) {
  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <TenantSwitcher tenants={consoleTenants} />
        <ContextSwitcher contexts={contexts} />
      </SidebarHeader>
      <SidebarContent className="gap-0">
        <NavGroups groups={navigation} />
      </SidebarContent>
      <DashboardSidebarFooter />
      <SidebarRail />
    </Sidebar>
  );
}
