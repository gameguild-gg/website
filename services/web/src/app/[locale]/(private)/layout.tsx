import React, { PropsWithChildren } from 'react';
// import Header from '@/components/common/header'
import Header from '@/components/header';

import { redirect } from 'next/navigation';
import { auth } from '@/auth';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  const session = await auth();

  if (!session?.user) {
    redirect('/connect');
    return null;
  }

  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
      <Header />
      {children}
      {/*<Footer/>*/}
    </div>
  );
}
