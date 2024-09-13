import {HttpClient} from '@/lib/core/http';
import {Project} from "@/lib/project/project";

export type CreateProject = {
  createProject: (project: Project) => Promise<Readonly<Project> | null>;
};

export class CreateProjectGateway implements CreateProject {
  constructor(readonly httpClient: HttpClient) {
  }

  public createProject(project: Project): Promise<Readonly<Project> | null> {
    // const game = GAMES.find((game) => game.slug === slug);

    return Promise.resolve(null);
  }
}
