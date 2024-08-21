import React from 'react';
import {fetchGames} from "@/lib/testing-lab/fetch-games.action";
import {fetchGame} from "@/lib/testing-lab/fetch-game.action";
import {PropsWithLocaleParams, PropsWithSlugParams} from "@/types";

type Props = PropsWithLocaleParams<PropsWithSlugParams>;

export async function generateStaticParams(): Promise<{ locale: string, slug: string }[]> {
  const games = await fetchGames();

  return games.map((game) => ({locale: '', slug: game.slug}));
}

export default async function Page({params: {locale, slug}}: Readonly<Props>) {
  const game = await fetchGame(slug);

  if (!game) throw Error(`not found.`);

  return (
    <div>
      <div>
        {game.slug}
      </div>
    </div>
  );
}
