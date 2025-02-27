import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';

export default async function Layout({
                                       children,
                                     }: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-auto flex-col bg-neutral-100">
      <Header />
      {children}
      {/*<Footer/>*/}
    </div>
  );
}
