'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';

export const dynamic = 'force-dynamic';

// todo: use the types from the api client!!
enum ContentTypes {
  ALL = 'All',
  GAMES = 'Games',
  ASSETS = 'Assets',
  BLOGS = 'Blogs',
  TESTS = 'Tests',
  JAMS = 'Jams',
  JOBS = 'Jobs',
  CODE_BATTLE = 'Code Battles',
}

const recentContent = [
  { id: 1, title: 'My Awesome Game', image: '/assets/images/placeholder.svg' },
  {
    id: 2,
    title: 'Cool 3D Asset Pack',
    image: '/assets/images/placeholder.svg',
  },
  {
    id: 3,
    title: 'How to Code Better',
    image: '/assets/images/placeholder.svg',
  },
  {
    id: 4,
    title: 'Python Skills Test',
    image: '/assets/images/placeholder.svg',
  },
  { id: 5, title: 'Game Jam Entry', image: '/assets/images/placeholder.svg' },
];

const viewsData = [
  { day: 'Day 1', views: 100 },
  { day: 'Day 2', views: 150 },
  { day: 'Day 3', views: 200 },
  { day: 'Day 4', views: 180 },
  { day: 'Day 5', views: 220 },
  { day: 'Day 6', views: 250 },
  { day: 'Day 7', views: 300 },
  { day: 'Day 8', views: 350 },
  { day: 'Day 9', views: 280 },
  { day: 'Day 10', views: 400 },
];

export default function CreatorDashboard() {
  const [activeTab, setActiveTab] = useState<ContentTypes>(ContentTypes.ALL);

  const router = useRouter();

  const handleCreateNewButton = (type: ContentTypes) => {
    switch (type) {
      case ContentTypes.JOBS:
        router.push('/jobs/post');
        break;
      case ContentTypes.GAMES:
        router.push('/projects/create');
        break;
      default:
        alert(
          'Not implemented yet! Help us develop this feature! Talk to us on Discord!',
        );
        break;
    }
  };

  const handleMoreButton = (type: ContentTypes) => {
    if (type == ContentTypes.JOBS) {
      router.push('/dashboard/jobs');
    }
  };

  return (
    <div className="container mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6">Creator Dashboard</h1>

      <Tabs
        value={activeTab}
        onValueChange={(str) => {
          setActiveTab(str as ContentTypes);
        }}
        className="w-full"
      >
        <TabsList className="grid grid-cols-4 lg:grid-cols-8 mb-6">
          {
            // iterate over all enum values
            Object.values(ContentTypes).map((type) => (
              <TabsTrigger key={type} value={type}>
                {type}
              </TabsTrigger>
            ))
          }
        </TabsList>

        {Object.values(ContentTypes).map((type) => (
          <TabsContent key={type} value={type} className="space-y-6">
            {type == ContentTypes.ALL && <div className="h-16"></div>}
            {type !== ContentTypes.ALL && (
              <Button
                onClick={() => handleCreateNewButton(type)}
                className="mb-6"
              >
                Create New {type.slice(0, -1)}
              </Button>
            )}

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div>
                <h2 className="text-2xl font-semibold mb-4">Recent</h2>
                <div className="space-y-4">
                  {recentContent.map((item) => (
                    <Card key={item.id} className="rounded-none">
                      <CardContent className="p-0 flex h-24">
                        <div className="relative w-24 h-full">
                          <Image
                            src={item.image}
                            alt={item.title}
                            layout="fill"
                            objectFit="cover"
                          />
                        </div>
                        <div className="flex-1 flex flex-col justify-between p-4">
                          <h3 className="font-medium">{item.title}</h3>
                          <div className="flex justify-end">
                            <Button variant="outline" size="sm">
                              Edit
                            </Button>
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </div>
              <div>
                <h2 className="text-2xl font-semibold mb-4">
                  Views (Last 10 Days)
                </h2>
                <div className="h-[300px]">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={viewsData}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="day" />
                      <YAxis />
                      <Tooltip />
                      <Bar dataKey="views" fill="#8884d8" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>
            </div>

            <div className="flex justify-start mt-6">
              <Button onClick={() => handleMoreButton(type)} variant="outline">
                More...
              </Button>
            </div>
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
