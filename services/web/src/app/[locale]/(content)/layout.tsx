import React, { PropsWithChildren } from 'react';
import Header from '@/components/common/header';
import { SessionProvider } from 'next-auth/react';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col">
      <Header />
      <SessionProvider>
        {children}
        {/*<Footer/>*/}
      </SessionProvider>
    </div>
  );
}
