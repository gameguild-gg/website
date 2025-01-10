import React, { ComponentType, PropsWithChildren, ReactElement } from 'react';
import { redirect } from 'next/navigation';
import { auth } from '@/auth';

export const dynamic = 'force-dynamic';

export function withAuth<TProps extends PropsWithChildren>(
  WrappedComponent: ComponentType<TProps>,
): ComponentType<TProps> {
  const AuthenticatedComponent = async (
    props: TProps,
  ): Promise<ReactElement | null> => {
    const session = await auth();

    if (!session?.user) {
      redirect('/connect');
    }
    return <WrappedComponent {...props} />;
  };

  AuthenticatedComponent.displayName = `withAuth(${WrappedComponent.displayName || WrappedComponent.name || 'Component'})`;

  return AuthenticatedComponent;
}
