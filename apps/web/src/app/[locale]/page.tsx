'use client';

import React from 'react';
import { useSession } from 'next-auth/react';

export default function Page(): React.JSX.Element {
  const { data: session, status } = useSession();

  // Isso vai mostrar no DevTools o objeto session que o front recebe
  console.log('Session on client:', session);
  console.log('Access Token:', session?.accessToken);
  if (status === 'loading') return <p>Carregando...</p>;
  if (!session) return <p>Você não está logado</p>;

  return (
    <div>
      <h1>Bem-vindo, {session.user?.name || 'Usuário'}</h1>
      <p>Access Token: {session.accessToken}</p>
      <pre>{JSON.stringify(session, null, 2)}</pre>
    </div>
  );
}
