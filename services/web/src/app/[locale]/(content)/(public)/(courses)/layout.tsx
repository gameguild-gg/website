import React, { PropsWithChildren } from 'react';

export default async function Layout({
                                       children,
                                     }: Readonly<PropsWithChildren>) {
  return <div className="flex flex-auto flex-col">{children}</div>;
}
