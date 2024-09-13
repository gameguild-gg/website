import {HttpClient} from '@/lib/core/http';
import {Project} from "@/lib/project/project";


export type FetchProjects = {
  fetchProjects: () => Promise<ReadonlyArray<Project>>;
};

export class FetchProjectsGateway implements FetchProjects {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchProjects(): Promise<ReadonlyArray<Project>> {
    return Promise.resolve([]);
  }
}
