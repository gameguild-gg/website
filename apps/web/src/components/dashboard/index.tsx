import React from 'react';
import DashboardHeader from '@/components/dashboard/dashboard-header';
import DashboardFooter from '@/components/dashboard/dashboard-footer';
import DashboardRoot from '@/components/dashboard/dashboard-root';
import DashboardSidebar from '@/components/dashboard/dashboard-sidebar';
import DashboardContent from '@/components/dashboard/dashboard-content';
import DashboardViewport from '@/components/dashboard/dashboard-viewport';

type Props = {
  children?: React.ReactNode;
};

const Dashboard: React.FunctionComponent<Readonly<Props>> & {
  Content: typeof DashboardContent;
  Footer: typeof DashboardFooter;
  Header: typeof DashboardHeader;
  Sidebar: typeof DashboardSidebar;
  Viewport: typeof DashboardViewport;
} = ({ children }: Readonly<Props>) => {
  return <DashboardRoot>{children}</DashboardRoot>;
};

Dashboard.Content = DashboardContent;
Dashboard.Footer = DashboardFooter;
Dashboard.Header = DashboardHeader;
Dashboard.Sidebar = DashboardSidebar;
Dashboard.Viewport = DashboardViewport;

export default Dashboard;
