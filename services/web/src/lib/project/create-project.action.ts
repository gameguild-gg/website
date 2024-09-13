'use server';

import {httpClientFactory} from '@/lib/core/http';
import {CreateProjectGateway} from "@/lib/project/create-project.gateway";
import {CreateProjectFormState} from "@/lib/project/create-project-form-state";


export async function createProject(previousState: CreateProjectFormState, formData: FormData): Promise<CreateProjectFormState> {
  const gateway = new CreateProjectGateway(httpClientFactory());

  // gateway.createProject(project);
  return Promise.resolve({});
}
