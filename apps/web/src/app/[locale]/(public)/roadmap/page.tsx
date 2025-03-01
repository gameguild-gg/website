import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  AlertTriangle,
  Bot,
  CheckCircle,
  Clock,
  LayoutDashboard,
  MonitorPlay,
  ReceiptText,
  Rss,
  Target,
  Users,
  Zap,
} from 'lucide-react';
import { ReactNode } from 'react';

export default function StartupRoadmap() {
  return (
    <div className="flex flex-col min-h-screen">
      <main className="flex-1">
        <section className="w-full py-12 md:py-24 lg:py-32 xl:py-16">
          <div className="container px-4 md:px-6">
            <div className="flex flex-col items-center space-y-4 text-center">
              <div className="space-y-2">
                <h1 className="text-3xl font-bold tracking-tighter sm:text-4xl md:text-5xl lg:text-6xl/none">
                  Game Guild Roadmap
                </h1>
                <p className="mx-auto max-w-[700px] text-gray-500 md:text-xl dark:text-gray-400">
                  Our journey from idea to successful Game Guild community.
                </p>
              </div>
            </div>
          </div>
        </section>
        <section className="w-full py-12 md:py-24 lg:py-16 bg-gray-100 dark:bg-gray-800">
          <div className="container px-4 md:px-6">
            <div className="grid gap-6 lg:grid-cols-3 lg:gap-12">
              <RoadmapItem
                icon={<Target className="h-10 w-10 text-blue-500" />}
                title="Idea Validation"
                description="Research market needs and validate our startup idea."
                status="done"
                date="2023-06-15"
              />
              <RoadmapItem
                icon={<Rss className="h-10 w-10 text-blue-500" />}
                title="Blog"
                description="Create a blog to Allow devs to publish content."
                status="done"
                date="2023-08-01"
              />
              <RoadmapItem
                icon={<Bot className="h-10 w-10 text-green-500" />}
                title="Chess Proof of Concept"
                description="Develop a chess competition for AI training."
                status="done"
                date="2024-01-04"
              />
              <RoadmapItem
                icon={<Users className="h-10 w-10 text-green-500" />}
                title="Login System"
                description="Login with google, metamask and email."
                status="done"
                date="2024-07-01"
              />
              <RoadmapItem
                icon={<Zap className="h-10 w-10 text-yellow-500" />}
                title="Game Testing Lab"
                description="Allow users to submit games to be tested."
                status="delayed"
                date="2024-07-01"
              />
              <RoadmapItem
                icon={<LayoutDashboard className="h-10 w-10 text-purple-500" />}
                title="Game Jam Manager"
                description="Allow users to enroll to Game Jams."
                status="in progress"
                date="2024-12-01"
              />
              <RoadmapItem
                icon={<MonitorPlay className="h-10 w-10 text-red-500" />}
                title="Courses"
                description="Hability to enroll, buy and sell courses."
                status="in progress"
                date="2025-01-01"
              />
              <RoadmapItem
                icon={<ReceiptText className="h-10 w-10 text-indigo-500" />}
                title="DAO"
                description="Descentralized Autonomous Organization."
                status="upcoming"
                date="2024-08-01"
              />
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}

interface RoadmapItemProps {
  icon: ReactNode;
  title: string;
  description: string;
  status: string;
  date: string;
}

// todo: fix typings. Type error: Binding element 'icon' implicitly has an 'any' type.
function RoadmapItem({
                       icon,
                       title,
                       description,
                       status,
                       date,
                     }: RoadmapItemProps) {
  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'done':
        return <Badge variant="default">Done</Badge>;
      case 'in progress':
        return <Badge variant="outline">In Progress</Badge>;
      case 'delayed':
        return <Badge variant="destructive">Delayed</Badge>;
      case 'upcoming':
        return <Badge variant="secondary">Upcoming</Badge>;
      default:
        return null;
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'done':
        return <CheckCircle className="h-5 w-5 text-green-500" />;
      case 'in progress':
        return <Clock className="h-5 w-5 text-yellow-500" />;
      case 'delayed':
        return <AlertTriangle className="h-5 w-5 text-red-500" />;
      case 'upcoming':
        return <Clock className="h-5 w-5 text-gray-300" />;
      default:
        return null;
    }
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-4">
          {icon}
          <div className="flex-1">
            <CardTitle>{title}</CardTitle>
            <CardDescription>{description}</CardDescription>
          </div>
          {getStatusBadge(status)}
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            {getStatusIcon(status)}
            <span className="text-sm capitalize">{status}</span>
          </div>
          <time dateTime={date} className="text-sm text-gray-500">
            {new Date(date).toLocaleDateString('en-US', {
              year: 'numeric',
              month: 'long',
              day: 'numeric',
            })}
          </time>
        </div>
      </CardContent>
    </Card>
  );
}
