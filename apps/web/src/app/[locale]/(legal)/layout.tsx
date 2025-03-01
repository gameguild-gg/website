// app/layout.tsx
import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';
import { PageContent } from '@/components/page/page-content';

export default function Layout({ children }: PropsWithChildren): JSX.Element {
  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
      <Header />
      <PageContent>{children}</PageContent>

    </div>
  );
}
