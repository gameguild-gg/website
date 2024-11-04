import React, { PropsWithChildren } from 'react';
// import Header from '@/components/common/header'
import Header from '@/components/header';
import { SessionProvider } from 'next-auth/react';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
        <Header />
        {children}
        {/*<Footer/>*/}
    </div>
}
