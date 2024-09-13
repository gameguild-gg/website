import {HttpClient} from '@/lib/core/http';
import {Project} from "@/lib/project/project";

export type FetchProject = {
  fetchProject: (slug: string) => Promise<Readonly<Project> | null>;
};

export class FetchProjectGateway implements FetchProject {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchProject(slug: string): Promise<Readonly<Project> | null> {
    // const game = GAMES.find((game) => game.slug === slug);

    return Promise.resolve(null);
  }
}
