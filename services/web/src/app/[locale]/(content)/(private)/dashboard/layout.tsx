import React, { PropsWithChildren } from 'react';
import Dashboard from '@/components/dashboard';

export default async function Layout({
                                       children,
                                     }: Readonly<PropsWithChildren>) {
  return (
    <>{children}</>
    // <Dashboard>
    //   <Dashboard.Sidebar />
    //   <Dashboard.Content size="compact">
    //     {/*<Dashboard.Header />*/}
    //     <Dashboard.Viewport></Dashboard.Viewport>
    //     {/*<Dashboard.Footer />*/}
    //   </Dashboard.Content>
    // </Dashboard>
    //<div className='w-full bg-gray-100'>{children}</div>
  );
}
