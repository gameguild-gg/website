import React, { PropsWithChildren } from 'react';
import { redirect } from 'next/navigation';
import { GalleryVerticalEnd } from 'lucide-react';
import { auth } from '@/auth';

export default async function Layout({ children }: PropsWithChildren): Promise<React.JSX.Element> {
  const session = await auth();

  if (session) redirect('/');

  return (
    <div className="flex flex-col flex-1 items-center justify-center gap-6 p-6 md:p-10 bg-muted">
      <div className="flex-col flex w-full max-w-sm gap-6">
        <a href="#" className="flex items-center gap-2 self-center font-medium">
          {/*TODO Change to the community logo*/}
          <div className="flex h-6 w-6 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <GalleryVerticalEnd className="size-4" />
          </div>
          Acme Inc.
        </a>
        {children}
      </div>
    </div>
  );
}
