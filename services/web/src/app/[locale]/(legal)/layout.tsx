import React, {PropsWithChildren} from 'react';
import Header from '@/components/header';
import { PageContent } from '@/components/page/page-content';
import { PageFooter } from '@/components/page/page-footer';

// export async function generateMetadata(parent: ResolvingMetadata): Promise<Metadata> {
//   return {};
// }
//
// export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
//   return [];
// }

export default async function Layout({children}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col bg-neutral-100">
      <Header />

      <PageContent>{children}</PageContent>
      
      <PageFooter />
    </div>
  );
}
