import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col">
      {children}
      {/*<Footer/>*/}
    </div>
  );
}
