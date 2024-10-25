import { PropsWithChildren } from 'react';
import { SessionProvider } from 'next-auth/react';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return <SessionProvider>{children}</SessionProvider>;
}
