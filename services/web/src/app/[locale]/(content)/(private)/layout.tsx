import { PropsWithChildren } from 'react';
import { auth } from '@/auth';

export const dynamic = 'force-dynamic';

export default async function Layout({ children }: Readonly<PropsWithChildren>) {
  const session = await auth();

  // if (!session) redirect('/connect');

  return (
    <>
      {children}
    </>
  );
}
