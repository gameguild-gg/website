'use server';

import {httpClientFactory} from '@/lib/core/http';
import {Game} from "@/lib/testing-lab/game";
import {FetchGameGateway} from "@/lib/testing-lab/fetch-game.gateway";

export async function fetchGame(slug: string): Promise<Readonly<Game | null>> {
  const gateway = new FetchGameGateway(httpClientFactory());

  return gateway.fetchGame(slug);
}
