import React from 'react';
import DashboardHeader from '@/components/dashboard/old/dashboard-header';
import DashboardFooter from '@/components/dashboard/old/dashboard-footer';
import DashboardRoot from '@/components/dashboard/old/dashboard-root';
import DashboardSidebar from '@/components/dashboard/old/dashboard-sidebar';
import DashboardContent from '@/components/dashboard/old/dashboard-content';
import DashboardViewport from '@/components/dashboard/old/dashboard-viewport';

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
