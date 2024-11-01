'use client'

import { useState } from 'react';
import Link from 'next/link';
import Image from 'next/image';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Play, PlayCircle, FileText, Lock } from 'lucide-react';

type Props = {
  params: {
    slug: string;
  };
};

export default function Component({ params: { slug } }: Readonly<Props>) {
  const [isPurchased, setIsPurchased] = useState(false);

  const handlePurchase = () => {
    setIsPurchased(true);
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="grid gap-6 lg:grid-cols-2">
        <div>
          <Image
            src="/assets/images/placeholder.svg"
            alt="Course Cover Image"
            width={600}
            height={400}
            className="rounded-lg object-cover w-full"
          />
        </div>
        <div className="flex flex-col justify-between">
          <div>
            <h1 className="text-4xl font-bold mb-4">Mastering React and Next.js</h1>
            <p className="text-xl text-muted-foreground mb-6">
              Learn to build modern web applications with React and Next.js. This comprehensive course covers
              everything from the basics to advanced topics, ensuring you're ready to tackle real-world projects.
            </p>
          </div>
          {!isPurchased && (
            <Button size="lg" onClick={handlePurchase}>
              Purchase Course for $99
            </Button>
          )}
        </div>
      </div>

      <Card className="mt-12">
        <CardHeader>
          <CardTitle>Content</CardTitle>
          <CardDescription>
            {isPurchased
              ? "You have full access to the course content. Enjoy learning!"
              : "Purchase the course to unlock all content."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ScrollArea className="h-[400px] w-full rounded-md border p-4">
            {courseContent.map((item, index) => (
              <div key={index} className="flex items-center mb-4 last:mb-0">
                {item.type === 'video' ? (
                  <PlayCircle className="mr-2 h-5 w-5 text-primary" />
                ) : (
                  <FileText className="mr-2 h-5 w-5 text-primary" />
                )}
                <span className="flex-grow">{item.title}</span>
                {isPurchased ? (
                  <Link href={`/courses/${slug}/${index}`}><Play className="ml-2 h-5 w-5 text-muted-foreground" /></Link>
                ) : (
                  <Lock className="ml-2 h-5 w-5 text-muted-foreground" />
                )}
              </div>
            ))}
          </ScrollArea>
        </CardContent>
      </Card>
    </div>
  )
}

const courseContent = [
  { type: 'video', title: 'Introduction to React' },
  { type: 'text', title: 'Setting up your development environment' },
  { type: 'video', title: 'Components and Props' },
  { type: 'video', title: 'State and Lifecycle' },
  { type: 'text', title: 'Handling Events in React' },
  { type: 'video', title: 'Introduction to Next.js' },
  { type: 'text', title: 'Routing in Next.js' },
  { type: 'video', title: 'Server-side Rendering with Next.js' },
  { type: 'video', title: 'API Routes in Next.js' },
  { type: 'text', title: 'Deploying your Next.js Application' },
  { type: 'video', title: 'Advanced React Patterns' },
  { type: 'text', title: 'Performance Optimization Techniques' },
  { type: 'video', title: 'Building a Full-stack Application' },
  { type: 'video', title: 'Course Wrap-up and Next Steps' },
] 