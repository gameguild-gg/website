'use client'

import { Project } from '@/app/[locale]/(content)/(private)/dashboard/(projects)/actions';
import { ProjectCard } from '@/app/[locale]/(content)/(private)/dashboard/(projects)/components/project-card';

type  Props = {
  projects: Project[]
}

export function ProjectList({ projects }: Props) {
  return (
    <>
      <header className="mb-8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">

      </header>
      <div className="grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 p-4">
        {projects.map(project => (
          <ProjectCard key={project.id} project={project} />
        ))}
      </div>
    </>
  );
}