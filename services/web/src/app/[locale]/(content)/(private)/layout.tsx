import { PropsWithChildren } from 'react';
import { auth } from '@/auth';
import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  const session = await auth();

  if (!session) redirect('/connect');

  return <>{children}</>;
}
