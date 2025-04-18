import React from 'react';
import Link from 'next/link';

export default async function Page() {
  return (<div>
    <Link href={`/gtl/feed`}>I want to test games</Link>

    <Link href={`/gtl/projects`}>I want my games tested!</Link>
  </div>);
}