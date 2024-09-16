import {HttpClient} from '@/lib/core/http';
import {Project} from "@/lib/project/project";


export type FetchProjects = {
  fetchProjects: () => Promise<ReadonlyArray<Project>>;
};

export class FetchProjectsGateway implements FetchProjects {
  constructor(readonly httpClient: HttpClient) {
  }

  public async fetchProjects(): Promise<ReadonlyArray<Project>> {
    try {
      return this.httpClient.request({
        url: 'http://localhost:4000/project',
        method: 'GET',
      }).then(response => {
        if (response.statusCode === 200 && response.body) {
          return response.body;
        } else {
          console.log(response);
          // throw new Error('Failed to fetch projects');
          return [];
        }
      });
    } catch (error) {
      throw error;
    }
  }
}
