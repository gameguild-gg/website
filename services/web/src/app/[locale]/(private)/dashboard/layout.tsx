import React from 'react';
import Dashboard from '@/components/dashboard';

type Props = {
  children: React.ReactNode;
};

export default async function Layout({ children }: Readonly<Props>) {
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
