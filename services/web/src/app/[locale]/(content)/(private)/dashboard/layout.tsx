import React, { PropsWithChildren } from 'react';
import Dashboard from '@/components/dashboard';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <Dashboard>
      <Dashboard.Sidebar />
      <Dashboard.Content size="compact">
        <Dashboard.Header />
        <Dashboard.Viewport>{children}</Dashboard.Viewport>
        <Dashboard.Footer />
      </Dashboard.Content>
    </Dashboard>
  );
}
