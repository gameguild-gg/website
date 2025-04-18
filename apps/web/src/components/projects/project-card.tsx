import { Clock, PauseCircle, Rocket } from 'lucide-react';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/components/ui/hover-card';
import Link from 'next/link';
import { Project } from '@/components/projects/actions';

function getStatusColor(status: Project['status']): string {
  switch (status) {
    case 'active':
      return 'bg-green-500';
    case 'in-progress':
      return 'bg-yellow-500';
    case 'not-started':
      return 'bg-gray-500';
    case 'on-hold':
      return 'bg-red-500';
    default:
      return 'bg-gray-500';
  }
}

type ProjectCardProps = Project;

function getStatusIcon(status: 'not-started' | 'in-progress' | 'active' | 'on-hold') {
  switch (status) {
    case 'active':
      return <Rocket className="w-4 h-4 text-green-500" />;
    case 'in-progress':
      return <Rocket className="w-4 h-4 text-yellow-500" />;
    case 'on-hold':
      return <PauseCircle className="w-4 h-4 text-red-500" />;
    case 'not-started':
    default:
      return <Clock className="w-4 h-4 text-gray-500" />;
  }
}

export function ProjectCard({ id, name, status, createdAt, updatedAt }: ProjectCardProps) {
  const handleClick = (e: React.MouseEvent<HTMLAnchorElement, MouseEvent>) => {
    if (e.target instanceof Element && e.target.closest('.hover-card-content')) {
      e.preventDefault();
    }
  };

  return (
    <Link href={`/projects/${id}`} className="block" onClick={handleClick}>
      <HoverCard>
        <HoverCardTrigger asChild>
          <div className="p-4 bg-zinc-900 rounded-lg hover:bg-zinc-800 transition-colors cursor-pointer">
            <div className="mb-2">
              <div className={`h-1 w-6 rounded ${getStatusColor(status)}`} />
            </div>
            <div className="space-y-1">
              <div className="flex justify-between items-center">
                <p className="text-zinc-400 text-sm uppercase tracking-wider">{status.replace('-', ' ')}</p>
                {getStatusIcon(status)}
              </div>
              <h3 className="text-zinc-100 font-medium">{name}</h3>
            </div>
          </div>
        </HoverCardTrigger>
        <HoverCardContent className="w-80 bg-zinc-800 border-zinc-700 text-white hover-card-content">
          <div className="space-y-2">
            <h4 className="text-sm font-semibold">{name}</h4>
            <p className="text-sm text-zinc-400">Status: {status.replace('-', ' ')}</p>
            <p className="text-sm text-zinc-400">Created: {createdAt || 'N/A'}</p>
            <p className="text-sm text-zinc-400">Last updated: {updatedAt || 'N/A'}</p>
          </div>
        </HoverCardContent>
      </HoverCard>
    </Link>
  );
}
