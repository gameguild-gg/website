import {HttpClient} from '@/lib/core/http';
import {Game} from "@/lib/testing-lab/game";
import {GAMES} from "@/lib/testing-lab/games.mock";

export type FetchGames = {
  fetchGames: () => Promise<ReadonlyArray<Game>>;
};

export class FetchGamesGateway implements FetchGames {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchGames(): Promise<ReadonlyArray<Game>> {
    return Promise.resolve(GAMES);
  }
}
