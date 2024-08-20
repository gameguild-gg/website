import {HttpClient} from '@/lib/core/http';
import {Game} from "@/lib/testing-lab/game";
import {GAMES} from "@/lib/testing-lab/games.mock";

export type FetchGame = {
  fetchGame: (slug: string) => Promise<Readonly<Game> | null>;
};

export class FetchGameGateway implements FetchGame {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchGame(slug: string): Promise<Readonly<Game> | null> {
    const game = GAMES.find((game) => game.slug === slug);

    return Promise.resolve(game ?? null);
  }
}
