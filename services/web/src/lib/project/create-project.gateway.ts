import { HttpClient } from '@/lib/core/http';
import { Project } from '@/lib/project/project';

export type CreateProject = {
  createProject: (project: Project) => Promise<Readonly<Project> | null>;
};

export class CreateProjectGateway implements CreateProject {
  constructor(readonly httpClient: HttpClient<Project>) {}

  public async createProject(
    project: Project,
  ): Promise<Readonly<Project> | null> {
    try {
      return this.httpClient
        .request({
          url: process.env.NEXT_PUBLIC_API_URL + '/project',
          method: 'POST',
          body: project,
        })
        .then((response) => {
          if (response.statusCode === 201 && response.body) {
            return response.body;
          } else {
            console.log(response);
            return null;
          }
        });
    } catch (error) {
      throw error;
    }
  }
}
