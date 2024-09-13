import {PropsWithChildren} from 'react';
import '@/styles/globals.css';
import {redirect} from 'next/navigation';
import {auth} from '@/auth';


export default async function Layout({children}: PropsWithChildren) {
  const session = await auth();

  if (!session) {
    redirect('/connect');
  }

  return children;
}
