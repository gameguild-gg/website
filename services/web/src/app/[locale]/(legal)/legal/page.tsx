import React from 'react';
import { ParamsWithLocale } from '@/types';


export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Page() {
  return (
    <div>
      <h1>Legal</h1>
    </div>
  );
}
