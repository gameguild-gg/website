import React from 'react';

type Props = {
  children: React.ReactNode;
};

export default async function Layout({ children }: Readonly<Props>) {
  return (<div>{children}</div>);
}
