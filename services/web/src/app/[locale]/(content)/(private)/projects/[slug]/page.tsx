'use client';
import React, { useEffect, useState } from 'react';
import { PropsWithLocaleParams, PropsWithSlugParams } from '@/types';
import { Api, ProjectApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { Session } from 'next-auth';

type Props = PropsWithLocaleParams<PropsWithSlugParams>;

export default function Page() {
  const [project, setProject] = useState<Api.ProjectEntity | null>(null);
  const [session, setSession] = useState<Session | null>();
  // get the slug from the URL. Last segment of the URL. /:lang/projects/:slug
  const slug: string = window.location.pathname.split('/').pop() || '';

  const getProject = async () => {
    const session = await getSession();
    setSession(session);
    const api = new ProjectApi({
      basePath: process.env.NEXT_PUBLIC_API_URL,
    });
    const game = await api.getOneBaseProjectControllerProjectEntity(
      { slug: slug },
      {
        headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
      },
    );
    if (game.status >= 400) {
      console.log(game.body);
    } else setProject(game.body as Api.ProjectEntity);
  };

  useEffect(() => {
    getProject().then();
  }, []);

  return (
    <div>
      <div>{JSON.stringify(project)}</div>
    </div>
  );
}
