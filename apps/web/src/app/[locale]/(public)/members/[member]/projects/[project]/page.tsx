import { cn } from "@game-guild/ui/lib/utils";
import { Badge } from '@game-guild/ui/components/badge';
import { buttonVariants } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { ArrowLeft, ExternalLink, User } from 'lucide-react';
import Image from 'next/image';
import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import React from 'react';
import { getMemberProject } from '@/lib/community/queries/members';

// Dynamic metadata for the public portfolio-project page; shares the request-scoped
// lookup with the page body.
export async function generateMetadata({
  params,
}: {
  params: Promise<{ member: string; project: string }>;
}): Promise<Metadata> {
  const { member, project } = await params;
  const result = await getMemberProject(member, project);

  if (!result) {
    return {
      title: 'Project Not Found',
      description: 'The requested portfolio project could not be found.',
      robots: { index: false, follow: false },
    };
  }

  const title = `${result.project.title} by ${result.member.displayName}`;
  const description =
    result.project.description?.trim().slice(0, 200) ||
    `${result.project.title} — a ${result.project.tech} project by ${result.member.displayName} on GameGuild.`;

  return {
    title,
    description,
    openGraph: {
      title,
      description,
      ...(result.project.imageUrl ? { images: [result.project.imageUrl] } : {}),
    },
  };
}

export default async function Page({
  params,
}: PageProps<'/[locale]/members/[member]/projects/[project]'>): Promise<React.JSX.Element> {
  const { locale, member, project } = await params;
  const result = await getMemberProject(member, project);

  if (!result) notFound();

  return (
    <main className="min-h-screen bg-slate-950 text-white">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-8">
        <Link
          href={`/${locale}/members/${member}`}
          className={cn(
            buttonVariants({
              variant: "ghost",
              className: "w-fit text-slate-300 hover:text-white",
            }),
          )}
        >
          <ArrowLeft className="mr-2 size-4" />
          Back to {result.member.displayName}
        </Link>

        <Card className="overflow-hidden border-purple-500/20 bg-slate-900/80">
          <div className="relative h-72 bg-gradient-to-br from-blue-700 to-purple-700">
            {result.project.imageUrl ? (
              <Image
                src={result.project.imageUrl}
                alt={result.project.title}
                fill
                className="object-cover"
                priority
                unoptimized
              />
            ) : (
              <div className="flex h-full w-full items-center justify-center px-6 text-center">
                <span className="text-lg font-semibold uppercase tracking-wide text-white/80">{result.project.title}</span>
              </div>
            )}
            <div className="absolute inset-0 bg-gradient-to-t from-slate-950/90 via-slate-950/30 to-transparent" />
            {result.project.isPinned ? (
              <Badge className="absolute left-5 top-5 bg-yellow-600 text-yellow-50">Featured</Badge>
            ) : null}
          </div>

          <CardHeader className="gap-3">
            <div className="flex flex-wrap items-center gap-3 text-sm text-slate-400">
              <Badge className="bg-purple-500/20 text-purple-100">{result.project.tech}</Badge>
              <span className="flex items-center gap-2">
                <User className="size-4" />
                {result.member.displayName}
              </span>
            </div>
            <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
              <CardTitle className="text-3xl text-white">{result.project.title}</CardTitle>
              {result.project.url ? (
                <a
                  href={result.project.url}
                  target="_blank"
                  rel="noreferrer"
                  className={cn(buttonVariants({ className: "w-fit" }))}
                >
                  <ExternalLink className="mr-2 size-4" />
                  Open project
                </a>
              ) : null}
            </div>
          </CardHeader>

          <CardContent className="space-y-6 text-slate-300">
            <p className="text-base leading-7">
              {result.project.description || 'This portfolio item is published on the member profile.'}
            </p>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
