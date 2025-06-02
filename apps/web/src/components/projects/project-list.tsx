'use client';

import { useMemo, useState } from 'react';

import { ArrowDownAZ, ArrowUpAZ, ChevronFirst, ChevronLast, ChevronLeft, ChevronRight, Inbox, PlusIcon, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Project } from './actions';
import { CreateProjectForm } from '@/components/projects/create-project-form';
import { ProjectCard } from '@/components/projects/project-card';

type SortField = 'name';
type SortDirection = 'asc' | 'desc';

interface ProjectListProps {
  initialProjects: Project[];
}

export function ProjectList({ initialProjects }: ProjectListProps) {
  const [projects, setProjects] = useState(initialProjects);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortField, setSortField] = useState<SortField>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [currentPage, setCurrentPage] = useState(1);
  const projectsPerPage = 6;

  const filteredAndSortedProjects = useMemo(() => {
    return projects
      .filter((project) => project.name.toLowerCase().includes(searchQuery.toLowerCase()))
      .sort((a, b) => {
        if (a[sortField] < b[sortField]) return sortDirection === 'asc' ? -1 : 1;
        if (a[sortField] > b[sortField]) return sortDirection === 'asc' ? 1 : -1;
        return 0;
      });
  }, [searchQuery, sortField, sortDirection, projects]);

  const totalPages = Math.ceil(filteredAndSortedProjects.length / projectsPerPage);
  const paginatedProjects = filteredAndSortedProjects.slice((currentPage - 1) * projectsPerPage, currentPage * projectsPerPage);

  const handleSort = (field: SortField) => {
    if (field === sortField) {
      setSortDirection((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const handleProjectCreated = (newProject: Project) => {
    setProjects((prev) => [...prev, newProject]);
  };

  return (
    <div className="flex flex-col flex-1 py-8">
      <header className="mb-8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold uppercase tracking-wider">Projects</h1>
          <p className="text-muted-foreground text-sm">All Teams • {filteredAndSortedProjects.length} projects</p>
        </div>
        <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4 w-full sm:w-auto">
          <div className="relative w-full sm:w-64">
            <Search className="absolute left-2 top-1/2 transform -translate-y-1/2 text-muted-foreground size-4" />
            <Input type="text" placeholder="Search projects..." className="pl-8" value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} />
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => handleSort('name')}>
              {sortDirection === 'asc' ? <ArrowUpAZ className="h-4" /> : <ArrowDownAZ className="h-4" />}
            </Button>
          </div>
          <CreateProjectForm onProjectCreated={handleProjectCreated} />
        </div>
      </header>

      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 h-full gap-4">
        {paginatedProjects.map((project) => (
          <ProjectCard
            key={project.id}
            id={project.id}
            name={project.name}
            status={project.status}
            createdAt={project.createdAt}
            updatedAt={project.updatedAt}
          />
        ))}
        {paginatedProjects.length === 0 && (
          <div className="flex flex-col flex-1 col-span-full items-center justify-center h-full text-center gap-4">
            <div className="flex flex-col items-center justify-center">
              <div className="p-6 rounded-full m-6 bg-muted-foreground/10">
                <Inbox className="size-12 text-muted-foreground" />
              </div>
              <h2 className="text-2xl font-bold mb-2">No projects yet</h2>
              <p className="text-muted-foreground max-w-md mb-6">
                Create your first project to get started. You can track progress, manage files, and collaborate with your team.
              </p>
            </div>
            <Button className="flex gap-4 px-4 py-2 align-center items-center justify-center bg-primary rounded-lg">
              <PlusIcon className="size-4" />
              Create your first project
            </Button>
          </div>
        )}
      </div>
      {paginatedProjects.length > 0 && (
        <div className="mt-8 flex justify-center items-center gap-1">
          <Button variant="outline" onClick={() => setCurrentPage(1)} disabled={currentPage === 1} className={`size-10`}>
            <ChevronFirst className="h-4 w-4" />
          </Button>
          <Button variant="outline" onClick={() => setCurrentPage((page) => Math.max(1, page - 1))} disabled={currentPage === 1} className={`size-10`}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <div className="flex items-center gap-2">
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
              <Button key={page} variant={currentPage === page ? 'default' : 'outline'} onClick={() => setCurrentPage(page)} className={`size-10`}>
                {page}
              </Button>
            ))}
          </div>
          <Button
            variant="outline"
            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
            disabled={currentPage === totalPages}
            className={`w-10 h-10 bg-zinc-900 border-zinc-800 text-zinc-400 hover:bg-zinc-800 hover:text-white`}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            onClick={() => setCurrentPage(totalPages)}
            disabled={currentPage === totalPages}
            className={`w-10 h-10 bg-zinc-900 border-zinc-800 text-zinc-400 hover:bg-zinc-800 hover:text-white`}
          >
            <ChevronLast className="h-4 w-4" />
          </Button>
        </div>
      )}
    </div>
  );
}
