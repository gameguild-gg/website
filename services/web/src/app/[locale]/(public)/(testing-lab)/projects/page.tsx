import React from 'react';
import Link from "next/link";
import {Button} from "@/components/ui/button";
import {GameCard} from "@/components/testing-lab/game-card";
import {fetchGames} from "@/lib/testing-lab/fetch-games.action";

export default async function Page() {
  const games = await fetchGames();

  if (games.length === 0) {
    return (
      <div>
        <p>It's looks like that you don't have a game to be tested yet.</p>
        <Button>Add a game</Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4 md:px-6">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:grid-cols-3 mt-8">
        {games.map((game) => (
          <Link key={game.slug} href={`projects/${game.slug}`}>
            <GameCard game={game}/>
          </Link>
        ))}
      </div>
    </div>
  );
}