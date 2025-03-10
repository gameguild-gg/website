import React from 'react';

import Header from '@/components/header';
import { PageContent } from '@/components/page/page-content';
import { PageFooter } from '@/components/page/page-footer';

type Props = {
  children: React.ReactNode;
};

export default function Layout({ children }: Readonly<Props>) {
  return (
    <div className="h-full w-full p-0 m-0">
      {/*<PageHeader/>*/}

      <Header />

      <PageContent>{children}</PageContent>

      <PageFooter />

      {/*<CookieConsent />*/}
    </div>
  );
}
