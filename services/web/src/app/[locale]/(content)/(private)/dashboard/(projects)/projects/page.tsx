import React from 'react';
import { getProjects } from '../actions';
import { ProjectList } from '@/app/[locale]/(content)/(private)/dashboard/(projects)/components/project-list';

export default async function Page() {
  const projects = await getProjects();

  return (
    <div>
      <ProjectList projects={projects} />
    </div>
  );
}
