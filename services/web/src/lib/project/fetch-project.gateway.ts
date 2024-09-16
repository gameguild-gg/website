import { HttpClient } from '@/lib/core/http';
import { Project } from '@/lib/project/project';

export type FetchProject = {
  fetchProject: (slug: string) => Promise<Readonly<Project> | null>;
};

export class FetchProjectGateway implements FetchProject {
  constructor(readonly httpClient: HttpClient) {}

  public fetchProject(slug: string): Promise<Readonly<Project> | null> {
    return Promise.resolve(null);
  }
}
