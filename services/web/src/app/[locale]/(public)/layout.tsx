import React from "react";

import { PageContent } from "@/components/page/page-content";
import { PageFooter } from "@/components/page/page-footer";
import { PageHeader } from "@/components/page/page-header";

import CookieConsent from "@/components/cookie/cookie-consent";


type Props = {
  children: React.ReactNode;
};

export default function Layout({ children }: Readonly<Props>) {
  return (
    <div className="h-full w-full p-0 m-0">

      <PageHeader />

      <PageContent>
        {children}
      </PageContent>

      <PageFooter />

      {/*<CookieConsent />*/}

    </div>
  );
}

