import React from 'react';
import {PropsWithSlugParams} from "@/types";


export default async function Page({params: {slug}}: Readonly<PropsWithSlugParams>) {
  return (
    <div>
      <h1>Courses</h1>
    </div>
  );
}
