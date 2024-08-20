import React from 'react';
import {fetchGames} from "@/lib/testing-lab/fetch-games.action";
import {fetchGame} from "@/lib/testing-lab/fetch-game.action";

type Props = {
  params: {
    slug: string;
  };
};

export async function generateStaticParams(): Promise<{ slug: string }[]> {
  const games = await fetchGames();

  return games.map((game) => ({slug: game.slug}));
}

export default async function Page({params: {slug}}: Readonly<Props>) {
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
