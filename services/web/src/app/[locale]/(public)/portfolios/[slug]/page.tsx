import React from 'react';
import type {Metadata, ResolvingMetadata} from 'next';

type Props = {
  params: {
    slug: string;
  };
};

export default async function Page({params: {slug}}: Readonly<Props>) {
  return <div></div>;
}

export async function generateMetadata(
  {params: {slug}}: Readonly<Props>,
  parent: ResolvingMetadata,
): Promise<Metadata> {
  return {};
}

export async function generateStaticParams() {
  return [];
}
