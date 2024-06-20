import {ReactNode} from 'react';
import '@/styles/globals.css';
import {redirect} from 'next/navigation';
import {auth} from '@/auth';


type Props = {
  children: ReactNode;
};

export default async function Layout({children}: Props) {
  const session = await auth();
  if (!session) {
    redirect('/sign-in');
  }

  return children;
}
