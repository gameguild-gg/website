import React, { PropsWithChildren } from 'react';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarSeparator,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import { Package, Send, Settings } from 'lucide-react';
import { cookies } from 'next/headers';

const data = {
  navigation: {
    primary: [
      {
        title: 'Projects',
        url: '/dashboard/projects',
        icon: Package,
      },
    ],
    secondary: [
      {
        title: 'Settings',
        url: '#',
        icon: Settings,
      },
      {
        title: 'Feedback',
        url: '#',
        icon: Send,
      },
    ],
  },
};

export const Dashboard = async ({ children }: PropsWithChildren) => {
  const cookieStore = await cookies();
  const defaultOpen = cookieStore.get('sidebar_state')?.value === 'true';

  return (
    <SidebarProvider defaultOpen={defaultOpen}>
      <DashboardSidebar />
      <SidebarInset className="flex flex-col flex-1">{children}</SidebarInset>
    </SidebarProvider>
  );
};

export const DashboardSidebar = ({ ...props }: React.ComponentProps<typeof Sidebar>) => {
  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader></SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarMenu>
            {data.navigation.primary.map((item) => (
              <SidebarMenuItem key={item.title}>
                <SidebarMenuButton asChild size="default" tooltip={item.title}>
                  <a href={item.url}>
                    <item.icon />
                    <span>{item.title}</span>
                  </a>
                </SidebarMenuButton>
              </SidebarMenuItem>
            ))}
          </SidebarMenu>
        </SidebarGroup>
      </SidebarContent>
      <SidebarFooter>
        <SidebarSeparator />
        <SidebarMenu>
          {data.navigation.secondary.map((item) => (
            <SidebarMenuItem key={item.title}>
              <SidebarMenuButton asChild size="default" tooltip={item.title}>
                <a href={item.url}>
                  <item.icon />
                  <span>{item.title}</span>
                </a>
              </SidebarMenuButton>
            </SidebarMenuItem>
          ))}
        </SidebarMenu>
        <SidebarSeparator />
        <div className="flex items-center justify-center">
          <SidebarTrigger />
        </div>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
};
