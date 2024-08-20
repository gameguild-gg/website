'use server';

import {httpClientFactory} from '@/lib/core/http';
import {FetchGamesGateway} from "@/lib/testing-lab/fetch-games.gateway";
import {Game} from "@/lib/testing-lab/game";

export async function fetchGames(): Promise<ReadonlyArray<Game>> {
  const gateway = new FetchGamesGateway(httpClientFactory());

  return gateway.fetchGames();
}
