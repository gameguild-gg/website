import { cn } from "@game-guild/ui/lib/utils";
import { CourseLandingPage } from '@/components/courses/course/course-landing-page';
import { buttonVariants } from '@game-guild/ui/components/button';
import { Link } from '@/i18n/navigation';
import { getProductsContainingCourse } from '@/lib/courses/actions/enrollment.actions';
import { getCourseViewerAccess } from '@/lib/courses/services/course-viewer-access';
import { getCachedPublicCourseBySlug } from '@/lib/courses/cached-public-course';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { notFound } from 'next/navigation';
import { Suspense } from 'react';

// Generate metadata for SEO
export async function generateMetadata({ params }: { params: Promise<{ course: string }> }) {
  const { course: slug } = await params;
  const result = await getCachedPublicCourseBySlug(slug);

  if (!result.success) {
    if (result.reason === 'unavailable') {
      return {
        title: 'Course Catalog Temporarily Unavailable | GameGuild',
        description: 'The learning catalog is temporarily unavailable. Please try again shortly.',
      };
    }

    return {
      title: 'Course Not Found',
      description: 'The requested course could not be found.',
    };
  }

  const course = result.data;

  if (!course) {
    notFound();
  }

  return {
    title: course.title,
    description: course.description,
    openGraph: {
      title: course.title,
      description: course.description,
      ...(course.thumbnail ? { images: [course.thumbnail] } : {}),
    },
  };
}

interface CourseDetailPageProps {
  readonly params: Promise<{ course: string }>;
}

function CourseUnavailableState({ error }: { readonly error?: string }) {
  return (
    <div className="min-h-screen bg-gray-950 px-4 py-16 text-white">
      <div className="mx-auto max-w-2xl rounded-2xl border border-amber-500/40 bg-slate-900/80 p-8 shadow-lg shadow-slate-950/40">
        <div className="flex items-start gap-4">
          <div className="rounded-full bg-amber-500/10 p-3 text-amber-300">
            <AlertTriangle className="h-6 w-6" />
          </div>
          <div className="space-y-4">
            <div className="space-y-2">
              <h1 className="text-2xl font-semibold">This course is temporarily unavailable</h1>
              <p className="text-gray-300">
                The storefront could not reach the learning API, so this page is not pretending the course was deleted or missing.
              </p>
            </div>
            {error ? <p className="text-sm text-amber-300">Latest error: {error}</p> : null}
            <div className="flex flex-wrap gap-3">
              <Link
                href="/courses"
                className={cn(
                  buttonVariants({ className: "bg-blue-600 text-white hover:bg-blue-500" }),
                )}
              >
                Back to catalog
              </Link>
              <Link
                href="/courses"
                className={cn(
                  buttonVariants({
                    variant: "outline",
                    className:
                      "border-slate-600 bg-slate-800/50 text-slate-100 hover:bg-slate-700/50 hover:text-white",
                  }),
                )}
              >
                Try again
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

async function CourseContent({ slug }: { slug: string }): Promise<React.JSX.Element> {
  const result = await getCachedPublicCourseBySlug(slug);

  if (!result.success) {
    if (result.reason === 'not-found') {
      notFound();
    }

    return <CourseUnavailableState error={result.error} />;
  }

  const course = result.data;

  if (!course) {
    notFound();
  }

  const [viewerAccess, products] = await Promise.all([
    course.id ? getCourseViewerAccess(String(course.id)) : Promise.resolve({ state: 'signed-out' as const }),
    getProductsContainingCourse(slug),
  ]);

  return <CourseLandingPage course={course} viewerAccess={viewerAccess} products={products} />;
}

export default async function CourseDetailPage({ params }: CourseDetailPageProps) {
  const { course: slug } = await params;

  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
          <div className="flex flex-col items-center space-y-4">
            <Loader2 className="h-8 w-8 animate-spin" />
            <p className="text-gray-400">Loading course...</p>
          </div>
        </div>
      }
    >
      <CourseContent slug={slug} />
    </Suspense>
  );
}
