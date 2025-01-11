import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col">
      <Header />
      <div className="flex flex-1 min-h-full">{children}</div>
    </div>
  );
}
