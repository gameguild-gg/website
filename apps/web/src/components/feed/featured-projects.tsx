import { getPublishedProjects } from '@/lib/projects/public-projects';
import { ArrowRight } from 'lucide-react';
import Link from 'next/link';
import React from 'react';

export async function FeaturedProjects(): Promise<React.JSX.Element> {
  const projects = (await getPublishedProjects()).slice(0, 5);

  return (
    <div className="rounded-3xl border border-border bg-card p-6 text-card-foreground">
      <h2 className="text-xl font-semibold">Featured projects</h2>
      <div className="mt-5 space-y-3">
        {projects.length === 0 ? (
          <p className="text-sm text-muted-foreground">Published projects will appear here.</p>
        ) : (
          projects.map((project) => (
            <Link
              key={project.slug}
              href={`/projects/${project.slug}`}
              className="group flex items-center justify-between gap-4 rounded-2xl border border-border bg-accent/30 p-4 transition hover:border-primary/30"
            >
              <span className="text-sm font-semibold text-foreground">{project.title}</span>
              <ArrowRight className="size-4 text-primary transition group-hover:translate-x-1" aria-hidden="true" />
            </Link>
          ))
        )}
      </div>
    </div>
  );
}
