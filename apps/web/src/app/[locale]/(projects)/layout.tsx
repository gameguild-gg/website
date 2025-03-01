import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';
import { auth } from '@/auth';
import { redirect } from 'next/navigation';

export default async function Layout({
                                       children,
                                     }: Readonly<PropsWithChildren>) {
  const session = await auth();

  if (!session) redirect('/connect');

  return (
    <div className="flex flex-1 flex-col">
      <Header />
      {children}
      {/*<Footer/>*/}
    </div>
  );
}
