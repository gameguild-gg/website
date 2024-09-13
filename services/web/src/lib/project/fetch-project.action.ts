'use server';

import {httpClientFactory} from '@/lib/core/http';
import {Project} from "@/lib/project/project";
import {FetchProjectGateway} from "@/lib/project/fetch-project.gateway";

export async function fetchProject(slug: string): Promise<Readonly<Project | null>> {
  const gateway = new FetchProjectGateway(httpClientFactory());

  return gateway.fetchProject(slug);
}
