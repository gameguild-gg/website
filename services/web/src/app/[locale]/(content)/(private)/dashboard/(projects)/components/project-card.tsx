import { Project } from '@/app/[locale]/(content)/(private)/dashboard/(projects)/actions';
import Link from 'next/link';
import { Clock, PauseCircle, Rocket } from 'lucide-react';

type Props = {
  project: Project
}

export function ProjectCard({ project }: Props) {
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

  const { name, slug, status } = project;
  return (
    // TODO: Move link to the props
    <Link href={`/dashboard/projects/${slug}`} className="block">
      <div className="p-4 bg-zinc-900 rounded-lg hover:bg-zinc-800 transition-colors cursor-pointer">
        <div className="mb-2">
          <div className={`h-1 w-6 rounded ${getStatusColor(status)}`} />
        </div>
        <div className="space-y-1">
          <div className="flex justify-between items-center">
            <p className="text-zinc-400 text-sm uppercase tracking-wider">
              {status.replace('-', ' ')}
            </p>
            {getStatusIcon(status)}
          </div>
          <h3 className="text-zinc-100 font-medium">{name}</h3>
        </div>
      </div>
    </Link>
  );
}