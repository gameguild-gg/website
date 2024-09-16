import React, {PropsWithChildren} from 'react';

// export async function generateMetadata(parent: ResolvingMetadata): Promise<Metadata> {
//   return {};
// }
//
// export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
//   return [];
// }

export default async function Layout({children}: Readonly<PropsWithChildren>) {
  return (
    <div>
      {children}
    </div>
  );
}
