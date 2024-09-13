'use server';

import {httpClientFactory} from '@/lib/core/http';
import {FetchProjectsGateway} from "@/lib/project/fetch-projects.gateway";
import {Project} from "@/lib/project/project";


export async function fetchProjects(): Promise<ReadonlyArray<Project>> {
  const gateway = new FetchProjectsGateway(httpClientFactory());

  return gateway.fetchProjects();
}
