import React, { PropsWithChildren } from 'react';
// import Header from '@/components/common/header'
import Header from '@/components/header';
import { withAuth } from '@/components/auth/with-auth';

async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
      <Header />
      {children}
      {/*<Footer/>*/}
    </div>
  );
}

export default withAuth(Layout);
