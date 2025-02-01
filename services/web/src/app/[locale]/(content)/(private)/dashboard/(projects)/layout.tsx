import React, { PropsWithChildren } from 'react';

export default async function Layout({ children }: Readonly<PropsWithChildren>) {
  return (
    <div>
      {children}
    </div>
  );
}
