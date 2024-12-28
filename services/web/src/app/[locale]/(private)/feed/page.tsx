'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Joystick,
  Box,
  FileText,
  FlaskConical,
  Trophy,
  Briefcase,
  Code,
} from 'lucide-react';
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationPrevious,
  PaginationLink,
  PaginationEllipsis,
  PaginationNext,
} from '@/components/ui/pagination';

// todo: I have no idea why this is necessary, if we are already stating use client
export const dynamic = 'force-dynamic';

const contentTypes = [
  { name: 'Games', icon: Joystick },
  { name: 'Assets', icon: Box },
  { name: 'Blogs', icon: FileText },
  { name: 'Tests', icon: FlaskConical },
  { name: 'Jams', icon: Trophy },
  { name: 'Jobs', icon: Briefcase },
  { name: 'Code Battles', icon: Code },
];

const contentItems = [
  {
    type: 'Games',
    title: 'Awesome Game',
    description: 'An exciting new game adventure',
  },
  {
    type: 'Assets',
    title: 'UI Kit',
    description: 'A comprehensive UI kit for game developers',
  },
  {
    type: 'Blogs',
    title: 'Game Dev Tips',
    description: 'Essential tips for game development',
  },
  {
    type: 'Tests',
    title: 'Performance Test',
    description: "Test your game's performance",
  },
  {
    type: 'Jams',
    title: 'Summer Game Jam',
    description: 'Join our summer game jam event',
  },
  {
    type: 'Jobs',
    title: 'Game Designer Wanted',
    description: "We're hiring a game designer",
  },
  {
    type: 'Code Battles',
    title: 'AI Challenge',
    description: 'Compete in our AI coding challenge',
  },
  {
    type: 'Games',
    title: 'RPG Adventure',
    description: 'Embark on an epic role-playing journey',
  },
  {
    type: 'Assets',
    title: 'Sound Pack',
    description: 'High-quality sound effects for your games',
  },
  {
    type: 'Blogs',
    title: 'Indie Success Stories',
    description: 'Learn from successful indie developers',
  },
  {
    type: 'Tests',
    title: 'Multiplayer Stress Test',
    description: 'Ensure your game can handle the load',
  },
  {
    type: 'Jams',
    title: 'Horror Game Jam',
    description: 'Create spine-chilling games in 48 hours',
  },
];

export default function ContentFeed() {
  const [selectedTypes, setSelectedTypes] = useState<string[]>([]);
  const [totalPages, setTotalPages] = useState<number>(1);

  const router = useRouter();

  const toggleContentType = (type: string) => {
    setSelectedTypes((prev) =>
      prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type],
    );
  };

  const handleCreateButtonClick = () => {
    router.push('/dashboard');
  };

  const filteredContent =
    selectedTypes.length > 0
      ? contentItems.filter((item) => selectedTypes.includes(item.type))
      : contentItems;

  return (
    <div className="flex min-h-[calc(100vh-80px)] container bg-white px-0">
      {/* Left side navigation */}
      <nav className="w-[260px] bg-gray-100 p-5 flex flex-col">
        <Button
          onClick={handleCreateButtonClick}
          className="mb-6 font-semibold"
        >
          CREATE
        </Button>
        <h2 className="text-lg font-semibold mb-4">Browse</h2>
        <div className="grid grid-cols-2 gap-4">
          {contentTypes.map((type) => (
            <div key={type.name} className="flex items-center space-x-2">
              <Checkbox
                id={type.name}
                checked={selectedTypes.includes(type.name)}
                onCheckedChange={() => toggleContentType(type.name)}
              />
              <label
                htmlFor={type.name}
                className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 cursor-pointer hover:underline"
              >
                {type.name}
              </label>
            </div>
          ))}
        </div>
      </nav>

      {/* Right side content */}
      <main className="flex-1 p-0">
        <ScrollArea className="h-full p-6">
          <section>
            <h2 className="text-2xl font-bold mb-4">Featured</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
              {filteredContent.slice(0, 4).map((item, index) => (
                <ContentCard key={index} item={item} />
              ))}
            </div>
          </section>
          <section>
            <h2 className="text-2xl font-bold mb-4">Latest</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {filteredContent.map((item, index) => (
                <ContentCard key={index} item={item} />
              ))}
            </div>
          </section>
          {/* Pagination */}
          <div className="flex justify-center mt-5">
            <Pagination>
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious href="#" />
                </PaginationItem>
                {[...Array(totalPages)]
                  .map((_, i) => (
                    <PaginationItem key={i}>
                      <PaginationLink href="#" isActive={i === 0}>
                        {i + 1}
                      </PaginationLink>
                    </PaginationItem>
                  ))
                  .slice(0, 3)}
                {totalPages > 3 && (
                  <>
                    <PaginationItem>
                      <PaginationEllipsis />
                    </PaginationItem>
                    <PaginationItem>
                      <PaginationLink href="#">{totalPages}</PaginationLink>
                    </PaginationItem>
                  </>
                )}
                <PaginationItem>
                  <PaginationNext href="#" />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          </div>
        </ScrollArea>
      </main>
    </div>
  );
}

function ContentCard({ item }: { item: (typeof contentItems)[0] }) {
  const Icon =
    contentTypes.find((type) => type.name === item.type)?.icon || Box;

  return (
    <Card className="overflow-hidden">
      <div className="aspect-video relative">
        <img
          src={`/assets/images/placeholder.svg?height=200&width=400`}
          alt={item.title}
          className="absolute inset-0 w-full h-full object-cover"
        />
      </div>
      <CardContent className="p-3">
        <div className="flex items-center space-x-2 mb-2">
          <Icon className="w-5 h-5" />
          <h3 className="font-semibold">{item.title}</h3>
        </div>
        <p className="text-sm text-gray-600">{item.description}</p>
      </CardContent>
    </Card>
  );
}
