import { HttpClient } from '@/lib/core/http';
import { Project } from '@/lib/project/project';

export type CreateProject = {
  createProject: (project: Project) => Promise<Readonly<Project> | null>;
};

export class CreateProjectGateway implements CreateProject {
  constructor(readonly httpClient: HttpClient<Project>) {
  }

  public async createProject(
    project: Project,
  ): Promise<Readonly<Project> | null> {
    try {
      return this.httpClient
        .request({
          url: 'http://localhost:8080/project',
          method: 'POST',
          body: project,
        })
        .then((response) => {
          if (response.statusCode === 201) {
            return response.body;
          } else {
            console.log(response);
            throw new Error('Failed to create project');
          }
        });
    } catch (error) {
      throw error;
    }
  }
}
