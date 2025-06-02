import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';

export default async function Layout({ children }: PropsWithChildren): Promise<React.JSX.Element> {
  return (
    <>
      <Header />
      {children}
    </>
  );
}
