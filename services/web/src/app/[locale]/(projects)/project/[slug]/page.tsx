'use client';
import React, { useEffect, useState } from 'react';
import { PropsWithLocaleSlugParams } from '@/types';
import { getSession } from 'next-auth/react';
import { Session } from 'next-auth';
import { Api, ProjectApi } from '@game-guild/apiclient';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CalendarIcon, Download, Edit, MessageSquare } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

// import type { Metadata, ResolvingMetadata } from 'next';
//
// export async function generateMetadata(
//   { params }: PropsWithLocaleSlugParams,
//   parent: ResolvingMetadata,
// ): Promise<Metadata> {
//   const session = await getSession();
//   const api = new ProjectApi({
//     basePath: process.env.NEXT_PUBLIC_API_URL,
//   });
//   const projectResponse = await api.getOneBaseProjectControllerProjectEntity(
//     { slug: params.slug },
//     {
//       headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
//     },
//   );
//   if (projectResponse.status >= 400) {
//     console.log(projectResponse.body);
//     return {};
//   }
//   const project = projectResponse.body as Api.ProjectEntity;
//
//   return {
//     title: project.title,
//     openGraph: {
//       images: [project.thumbnail],
//     },
//     description: project.summary,
//   };
// }

// get locale and slug from the URL
export default function Page(params: PropsWithLocaleSlugParams) {
  const { slug, locale } = params.params;
  const [project, setProject] = useState<Api.ProjectEntity | null>(null);
  const [session, setSession] = useState<Session | null>();
  const router = useRouter();
  const [latestVersion, setLastVersion] =
    useState<Api.ProjectVersionEntity | null>(null);

  // button to edit visible if the user is the owner of the project
  const IsOwnerButton = () => {
    if (session?.user?.id === project?.owner?.id) {
      // redirect to `/project/${slug}/edit`
      return (
        <Button
          className="w-full mb-4 text-lg py-6"
          size="lg"
          onClick={() => router.push(`/project/${slug}/edit`)}
        >
          <Edit className="mr-2" /> Edit Project
        </Button>
      );
    }
    return null;
  };

  const SubmitNewVersionButton = () => {
    if (session?.user?.id === project?.owner?.id) {
      return (
        <Button
          className="w-full mb-4 text-lg py-6"
          size="lg"
          onClick={() => router.push(`/project/${slug}/edit`)}
        >
          <Edit className="mr-2" /> Submit New Version
        </Button>
      );
    }
    return null;
  };

  const getProject = async () => {
    const session = await getSession();
    setSession(session);
    const api = new ProjectApi({
      basePath: process.env.NEXT_PUBLIC_API_URL,
    });
    const game = await api.getOneBaseProjectControllerProjectEntity(
      { slug: slug, join: ['owner'] },
      {
        headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
      },
    );
    if (game.status >= 400) {
      console.log(game.body);
    }
    setProject(game.body as Api.ProjectEntity);
    // last version is the last element in the array
    setLastVersion(project?.versions?.at(-1) || null);
  };

  useEffect(() => {
    getProject().then();
  }, []);

  return (
    <div className="min-h-screen bg-background">
      {/* Banner */}
      <div className="relative h-64 md:h-96">
        <Image
          src="https://placehold.co/400x1200.svg"
          alt="Game Banner"
          layout="fill"
          objectFit="cover"
          className="brightness-50"
        />
        <div className="absolute inset-0 flex items-center justify-center">
          <h1 className="text-4xl md:text-6xl font-bold text-white">
            {project?.title}
          </h1>
        </div>
      </div>

      <main className="container mx-auto px-4 py-8">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div className="md:col-span-2">
            {/* Featured Video and Images */}
            <div className="mb-8">
              {/* Video */}
              <div className="aspect-video mb-4">
                <iframe
                  width="100%"
                  height="100%"
                  src="https://www.youtube.com/embed/dQw4w9WgXcQ"
                  title="Game Trailer"
                  frameBorder="0"
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                  allowFullScreen
                  className="rounded-lg"
                ></iframe>
              </div>

              {/* Images */}
              <div className="grid grid-cols-2 gap-4">
                <Image
                  src="https://placehold.co/300x200.svg"
                  alt="Featured Image 1"
                  width={300}
                  height={200}
                  className="rounded-lg"
                />
                <Image
                  src="https://placehold.co/300x200.svg"
                  alt="Featured Image 2"
                  width={300}
                  height={200}
                  className="rounded-lg"
                />
                <Image
                  src="https://placehold.co/300x200.svg"
                  alt="Featured Image 3"
                  width={300}
                  height={200}
                  className="rounded-lg"
                />
                <Image
                  src="https://placehold.co/300x200.svg"
                  alt="Featured Image 4"
                  width={300}
                  height={200}
                  className="rounded-lg"
                />
              </div>
            </div>

            {/* Game Description */}
            <Card className="mb-8">
              <CardHeader>
                <CardTitle>About {project?.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <p>{project?.body}</p>
              </CardContent>
            </Card>

            {/* Previous Versions */}
            <Card>
              <CardHeader>
                <CardTitle>Versions</CardTitle>
              </CardHeader>
              <CardHeader>
                <SubmitNewVersionButton />
              </CardHeader>
              <CardContent>
                <ul className="space-y-2">
                  {project?.versions?.map((version, index) => (
                    <li
                      key={index}
                      className="flex justify-between items-center"
                    >
                      <span>{version.version}</span>
                      <span className="text-muted-foreground">
                        {version.createdAt}
                      </span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          </div>

          <div>
            {/* Edit Button */}
            <IsOwnerButton />
            {/* Download Button */}
            <Button className="w-full mb-4 text-lg py-6" size="lg">
              <Download className="mr-2" /> Download {latestVersion?.version}
            </Button>

            {/* Latest Release Info */}
            <Card className="mb-4">
              <CardHeader>
                <CardTitle>Latest Release</CardTitle>
              </CardHeader>
              <CardContent>
                <p>
                  <strong>Version:</strong> {latestVersion?.version}
                </p>
                <p>
                  <strong>Release Date:</strong> {latestVersion?.createdAt}
                </p>
                <p>
                  <strong>Developer:</strong> {project?.owner?.username}
                </p>
              </CardContent>
            </Card>

            {/* Feedback Link */}
            <Card className="mb-4">
              <CardHeader>
                <CardTitle>Feedback</CardTitle>
              </CardHeader>
              <CardContent>
                <Link
                  href="#"
                  className="flex items-center text-primary hover:underline"
                >
                  <MessageSquare className="mr-2" /> Submit Feedback
                </Link>
                <p className="mt-2 text-sm text-muted-foreground">
                  <CalendarIcon className="inline mr-1" />
                  Deadline: {latestVersion?.feedback_deadline}
                </p>
              </CardContent>
            </Card>
          </div>
        </div>
      </main>
    </div>
  );
}
