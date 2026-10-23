'use client';

import { cn } from "@game-guild/ui/lib/utils";
import { Badge } from '@game-guild/ui/components/badge';
import { Button, buttonVariants } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Link } from '@/i18n/navigation';
import type { Program } from '@/lib/api/generated';
import { getCourseCategoryName, getCourseLevelConfig } from '@/lib/courses/services/course.service';
import { PUBLIC_PROGRAM_PACKAGES, getCourseShowcase, getProgramForCourse } from '@/lib/courses/public-programs';
import { ArrowRight, Clock, Layers3, RotateCcw, Search } from 'lucide-react';
import Image from 'next/image';
import { useSearchParams } from 'next/navigation';
import React from 'react';

interface PublicCourseCatalogProps {
  initialCourses: Program[];
}

function normalizeFilterValue(value: string): string {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}

function getCourseSlug(course: Program): string | null {
  return typeof course.slug === 'string' && course.slug.length > 0 ? course.slug : null;
}

function getCourseImage(course: Program): string | null {
  return typeof course.thumbnail === 'string' && course.thumbnail.length > 0 ? course.thumbnail : null;
}

function getCourseTitle(course: Program): string {
  return typeof course.title === 'string' && course.title.length > 0 ? course.title : 'Untitled course';
}

function getCourseDescription(course: Program): string {
  return typeof course.description === 'string' && course.description.length > 0
    ? course.description
    : 'A published GameGuild course ready for students to explore.';
}

function courseMatchesText(course: Program, query: string): boolean {
  const courseSlug = getCourseSlug(course);
  const haystack = [
    course.title,
    course.description,
    course.slug,
    getCourseCategoryName(course.category as string | number | null | undefined),
    getProgramForCourse(courseSlug)?.title,
    getCourseShowcase(courseSlug)?.headline,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();

  return haystack.includes(query.toLowerCase());
}

function CourseCard({ course }: { course: Program }) {
  const courseTitle = getCourseTitle(course);
  const courseSlug = getCourseSlug(course);
  const courseDescription = getCourseDescription(course);
  const courseImage = getCourseImage(course);
  const categoryName = getCourseCategoryName(course.category as string | number | null | undefined);
  const level = getCourseLevelConfig(course.difficulty as string | number | null | undefined).name;
  const estimatedHours = typeof course.estimatedHours === 'number' ? course.estimatedHours : null;
  const program = getProgramForCourse(courseSlug);
  const showcase = getCourseShowcase(courseSlug);
  const outcome = showcase?.projectResult ?? courseDescription;

  return (
    <article className="group grid overflow-hidden rounded-2xl border border-white/10 bg-white/[0.04] transition hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/[0.065] md:grid-cols-[160px_1fr]">
      <Link href={courseSlug ? `/courses/${courseSlug}` : '/courses'} className="relative min-h-[170px] overflow-hidden bg-slate-900 md:min-h-full">
        {courseImage ? (
          <Image
            src={courseImage}
            alt={courseTitle}
            fill
            loading="eager"
            unoptimized={courseImage.endsWith('.svg')}
            className="object-cover opacity-90 transition duration-500 group-hover:scale-105"
            sizes="(min-width: 1280px) 160px, (min-width: 768px) 30vw, 100vw"
          />
        ) : (
          <div className="h-full bg-[linear-gradient(135deg,#020617,#111827_52%,#1e1b4b)]" />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-[#070a12]/65 via-transparent to-transparent" />
      </Link>

      <div className="flex min-w-0 flex-col gap-4 p-5">
        <div className="flex flex-wrap gap-2">
          <Badge variant="secondary" className="bg-white/10 text-white">
            {program?.shortTitle ?? categoryName}
          </Badge>
          <Badge variant="outline" className="border-white/15 text-slate-300">
            {level}
          </Badge>
          {estimatedHours ? (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 px-2.5 py-0.5 text-xs text-slate-300">
              <Clock className="size-3.5" />
              {estimatedHours}h
            </span>
          ) : null}
        </div>

        <div className="min-w-0">
          <Link href={courseSlug ? `/courses/${courseSlug}` : '/courses'} className="block text-xl font-semibold leading-tight tracking-tight text-white hover:text-sky-100">
            {courseTitle}
          </Link>
          <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-400">{outcome}</p>
        </div>

        <div className="mt-auto flex items-center justify-between gap-3 border-t border-white/10 pt-4">
          <p className="truncate text-xs uppercase tracking-[0.16em] text-slate-500">{categoryName}</p>
          {courseSlug ? (
            <Link
              href={`/courses/${courseSlug}`}
              className={cn(
                buttonVariants({
                  size: "sm",
                  variant: "ghost",
                  className: "shrink-0 text-white hover:bg-white/10 hover:text-white",
                }),
              )}
            >
              Details
              <ArrowRight />
            </Link>
          ) : null}
        </div>
      </div>
    </article>
  );
}

export function PublicCourseCatalog({ initialCourses }: PublicCourseCatalogProps) {
  const searchParams = useSearchParams();
  const urlCategoryFilter = searchParams?.get('category');
  const urlProgramFilter = searchParams?.get('program');
  const [query, setQuery] = React.useState('');
  const [activeProgram, setActiveProgram] = React.useState(urlProgramFilter ?? 'all');
  const [activeCategory, setActiveCategory] = React.useState(urlCategoryFilter ?? 'all');

  const categories = React.useMemo(
    () => [
      'all',
      ...Array.from(
        new Set(
          initialCourses
            .map((course) => getCourseCategoryName(course.category as string | number | null | undefined))
            .filter(Boolean),
        ),
      ),
    ],
    [initialCourses],
  );

  const visibleCourses = React.useMemo(() => {
    const normalizedCategory = normalizeFilterValue(activeCategory);

    return initialCourses.filter((course) => {
      const slug = getCourseSlug(course);
      const program = getProgramForCourse(slug);
      const categoryName = getCourseCategoryName(course.category as string | number | null | undefined);

      if (activeProgram !== 'all' && program?.slug !== activeProgram) {
        return false;
      }

      if (normalizedCategory !== 'all' && normalizeFilterValue(categoryName) !== normalizedCategory) {
        return false;
      }

      if (query.trim() && !courseMatchesText(course, query.trim())) {
        return false;
      }

      return true;
    });
  }, [activeCategory, activeProgram, initialCourses, query]);

  const hasActiveFilters = query.trim() !== '' || activeProgram !== 'all' || activeCategory !== 'all';

  const resetFilters = () => {
    setQuery('');
    setActiveProgram('all');
    setActiveCategory('all');
  };

  return (
    <div className="container mx-auto flex flex-col gap-8 px-4 py-14">
      <div className="grid gap-6 lg:grid-cols-[0.75fr_1.25fr] lg:items-end">
        <div>
          <h2 className="text-4xl font-semibold tracking-tight text-white md:text-5xl">Browse courses</h2>
          <p className="mt-4 max-w-xl text-base leading-7 text-slate-400">
            Compact course cards, organized by package and discipline. Open a course to review its full landing page and curriculum context.
          </p>
        </div>

        <div className="flex flex-col gap-3">
          <div className="relative">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500" aria-hidden="true" />
            <Input
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search courses, tools, or outcomes..."
              aria-label="Search courses by title, tool, or outcome"
              className="h-12 rounded-2xl border-white/10 bg-white/[0.04] pl-11 text-white placeholder:text-slate-500"
            />
          </div>

          <div className="flex flex-wrap gap-2" role="group" aria-label="Filter courses by package">
            <Button size="sm" aria-pressed={activeProgram === 'all'} variant={activeProgram === 'all' ? 'default' : 'outline'} onClick={() => setActiveProgram('all')} className={activeProgram === 'all' ? 'bg-white text-slate-950 hover:bg-slate-200' : 'border-white/10 bg-transparent text-slate-200 hover:bg-white/10 hover:text-white'}>
              All packages
            </Button>
            {PUBLIC_PROGRAM_PACKAGES.map((program) => (
              <Button key={program.slug} size="sm" aria-pressed={activeProgram === program.slug} variant={activeProgram === program.slug ? 'default' : 'outline'} onClick={() => setActiveProgram(program.slug)} className={activeProgram === program.slug ? 'bg-white text-slate-950 hover:bg-slate-200' : 'border-white/10 bg-transparent text-slate-200 hover:bg-white/10 hover:text-white'}>
                {program.shortTitle}
              </Button>
            ))}
          </div>

          <div className="flex flex-wrap gap-2" role="group" aria-label="Filter courses by discipline">
            {categories.map((category) => (
              <Button
                key={category}
                size="sm"
                aria-pressed={activeCategory === category}
                variant={activeCategory === category ? 'secondary' : 'ghost'}
                onClick={() => setActiveCategory(category)}
                className={activeCategory === category ? 'bg-slate-200 text-slate-950 hover:bg-white' : 'text-slate-300 hover:bg-white/10 hover:text-white'}
              >
                {category === 'all' ? 'All disciplines' : category}
              </Button>
            ))}
          </div>
        </div>
      </div>

      <div className="flex items-center justify-between gap-4 border-y border-white/10 py-4 text-sm text-slate-400">
        <span aria-live="polite">
          {visibleCourses.length} matching {visibleCourses.length === 1 ? 'course' : 'courses'}
        </span>
        {hasActiveFilters ? (
          <Button size="sm" variant="ghost" onClick={resetFilters} className="text-slate-300 hover:bg-white/10 hover:text-white">
            Reset filters
            <RotateCcw className="size-4" />
          </Button>
        ) : (
          <Link href="/learn/courses" className="inline-flex items-center gap-2 font-medium text-slate-200 underline-offset-4 hover:text-white hover:underline">
            My enrolled courses
            <Layers3 className="size-4" />
          </Link>
        )}
      </div>

      {initialCourses.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/15 bg-white/[0.035] p-10 text-center text-slate-300">
          <p className="text-lg font-semibold text-white">No courses are published yet.</p>
          <p className="mt-2 text-sm">
            Published courses appear here as soon as they are listed. Come back soon or explore the learning programs.
          </p>
        </div>
      ) : visibleCourses.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/15 bg-white/[0.035] p-10 text-center text-slate-300">
          <p className="text-lg font-semibold text-white">No courses matched this view.</p>
          <p className="mt-2 text-sm">Clear filters or search for another discipline.</p>
          <Button size="sm" variant="outline" onClick={resetFilters} className="mt-5 border-white/15 bg-transparent text-white hover:bg-white/10 hover:text-white">
            Clear filters
          </Button>
        </div>
      ) : (
        <div className="grid gap-5 xl:grid-cols-2">
          {visibleCourses.map((course) => (
            <CourseCard key={course.id ?? course.slug} course={course} />
          ))}
        </div>
      )}
    </div>
  );
}
