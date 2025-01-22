// app/layout.tsx
import React, { PropsWithChildren } from 'react';
import Header from '@/components/header';
import { PageContent } from '@/components/page/page-content';
import { PageFooter } from '@/components/page/page-footer';

export default function Layout({ children }: PropsWithChildren): JSX.Element {
  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
      <Header />
      <PageContent>{children}</PageContent>
      
    </div>
  );
}
