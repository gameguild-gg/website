import React from 'react';
import { ParamsWithLocale, PropsWithSlugParams } from '@/types';
import type { Metadata, ResolvingMetadata } from 'next';

// export async function generateMetadata(parent: ResolvingMetadata): Promise<Metadata> {
//   return {};
// }

// export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
//   return [];
// }

export default async function Page({
  params: { slug },
}: Readonly<PropsWithSlugParams>) {
  return (
    <div>
      <h1>Courses</h1>
    </div>
  );
}
