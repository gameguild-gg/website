'use server';

import {httpClientFactory} from '@/lib/core/http';
import {CreateProjectGateway} from "@/lib/project/create-project.gateway";
import {CreateProjectFormState} from "@/lib/project/create-project-form-state";
import {VisibilityEnum} from "@/lib/project/project";


export async function createProject(previousState: CreateProjectFormState, formData: FormData): Promise<CreateProjectFormState> {
  const gateway = new CreateProjectGateway(httpClientFactory());

  const slug = formData.get('slug')?.toString().trim();
  const title = formData.get('title')?.toString().trim();
  const summary = formData.get('summary')?.toString().trim();
  const body = formData.get('body')?.toString().trim();
  const thumbnail = formData.get('thumbnail')?.toString().trim();

  const errors: CreateProjectFormState['errors'] = {};
  if (!title) errors.projectName = 'Title is required';
  if (!slug) errors.slug = 'Slug is required';
  if (!summary) errors.summary = 'Summary is required';
  if (!thumbnail) errors.thumbnail = 'Thumbnail URL is required';
  if (!body) errors.body = 'Body is required';

  if (Object.keys(errors).length > 0) {
    return {errors};
  }

  // Criar o objeto do projeto
  const project = {
    title,
    slug,
    summary,
    thumbnail,
    visibility: VisibilityEnum.DRAFT,
    body,
  };

  try {
    // Enviar o projeto para o backend
    const createdProject = await gateway.createProject(project);
    return {project: createdProject ?? undefined};
  } catch (error: any) {
    return {errors: {general: error.message || 'An error occurred'}};
  }
}
