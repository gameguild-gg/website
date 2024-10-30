import React, { PropsWithChildren } from 'react';
import Header from '@/components/common/header';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col">
      <Header />
      {children}
      {/*<Footer/>*/}
    </div>
  );
}
