import React, {PropsWithChildren} from 'react';
import type {Metadata, ResolvingMetadata} from 'next'
import {ParamsWithLocale} from "@/types";
import Header from "@/components/common/header";

export async function generateMetadata(parent: ResolvingMetadata): Promise<Metadata> {
  return {};
}

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Layout({children}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 flex-col min-h-full">
      <Header></Header>
      {children}

    </div>
  );
}
