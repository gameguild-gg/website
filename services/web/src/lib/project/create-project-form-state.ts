import {Project} from "@/lib/project/project";

export type CreateProjectFormState = {
  errors?: {
    general?: string;
    projectName?: string;
    slug?: string;
    summary?: string;
    thumbnail?: string;
    body?: string;
  };
  project?: Project;
};
