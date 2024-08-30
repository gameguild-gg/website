import React, {PropsWithChildren} from 'react';
import type {Metadata, ResolvingMetadata} from 'next'
import {ParamsWithLocale} from "@/types";

export async function generateMetadata(parent: ResolvingMetadata): Promise<Metadata> {
  return {};
}

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Layout({children}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 min-h-full">
      {children}
    </div>
  );
}
