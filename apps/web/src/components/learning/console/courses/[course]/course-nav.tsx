"use client";

import { cn } from "@game-guild/ui/lib/utils";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { buildDashboardCoursePath } from "@/lib/learning/course-route";
import type { CourseFeatures } from "@/lib/learning/types";
import {
  publishCourse,
  restoreCourse,
  unpublishCourse,
} from "@/lib/learning/actions";
import { Badge } from "@game-guild/ui/components/badge";
import { Button, buttonVariants } from "@game-guild/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
import {
  ArrowLeft,
  Award,
  BookOpen,
  CalendarDays,
  Eye,
  EyeOff,
  GraduationCap,
  Info,
  LayoutDashboard,
  Loader2,
  MessageSquare,
  Settings,
  Share2,
  Users,
  UsersRound,
  type LucideIcon,
} from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import { useLearningBase } from "@/lib/learning/use-learning-base";
import type { CourseAccessCapabilities } from "@/lib/learning/queries/course";

interface CourseNavProps {
  courseTitle: string;
  courseDescription: string;
  courseStatus: "draft" | "published" | "archived";
  courseSlug: string | null;
  courseRouteParam: string;
  locale: string;
  features: CourseFeatures;
  access: CourseAccessCapabilities;
  children: ReactNode;
}

interface NavItem {
  title: string;
  icon: LucideIcon;
  segment: string;
  enabled: boolean;
}

function getStatusBadge(status: string) {
  switch (status) {
    case "published":
      return (
        <Badge className="bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
          Published
        </Badge>
      );
    case "draft":
      return <Badge variant="secondary">Draft</Badge>;
    case "archived":
      return <Badge variant="outline">Archived</Badge>;
    default:
      return null;
  }
}

function buildNavItems(
  features: CourseFeatures,
  access: CourseAccessCapabilities,
): NavItem[] {
  return [
    {
      title: "Overview",
      icon: LayoutDashboard,
      segment: "overview",
      enabled: true,
    },
    {
      title: "Listing",
      icon: Info,
      segment: "listing",
      enabled: access.canEdit || access.canPublish,
    },
    {
      title: "Content",
      icon: BookOpen,
      segment: "content",
      enabled: access.canEdit,
    },
    {
      title: "Classes",
      icon: CalendarDays,
      segment: "classes",
      enabled: access.canEdit && features.hasClasses,
    },
    {
      title: "Assessments",
      icon: GraduationCap,
      segment: "assessments",
      enabled:
        features.hasAssessments && (access.canEdit || access.canReviewAsStaff),
    },
    {
      title: "Certificates",
      icon: Award,
      segment: "certificates",
      enabled: access.canEdit && features.hasCertificate,
    },
    {
      title: "Groups",
      icon: UsersRound,
      segment: "groups",
      enabled: access.canEdit,
    },
    {
      title: "Students",
      icon: Users,
      segment: "students",
      enabled: access.canReviewAsStaff,
    },
    {
      title: "Support",
      icon: MessageSquare,
      segment: "support",
      enabled: access.canEdit,
    },
    {
      title: "Settings",
      icon: Settings,
      segment: "settings",
      enabled: access.canEdit,
    },
  ].filter((item) => item.enabled);
}

export function CourseNav({
  courseTitle,
  courseDescription,
  courseStatus,
  courseSlug,
  courseRouteParam,
  locale,
  features,
  access,
  children,
}: CourseNavProps) {
  const learningBase = useLearningBase();
  const pathname = usePathname() ?? "";
  const router = useRouter();
  const publicCourseHref = courseSlug?.trim()
    ? `/courses/${courseSlug.trim()}`
    : null;
  const previewHref = buildDashboardCoursePath(courseRouteParam, "preview");
  const [shareLabel, setShareLabel] = useState("Share");
  const [status, setStatus] = useState(courseStatus);
  const [isPending, setIsPending] = useState(false);
  const [showUnpublishDialog, setShowUnpublishDialog] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const navItems = buildNavItems(features, access);

  useEffect(() => {
    setStatus(courseStatus);
  }, [courseStatus]);

  async function copyPublicCourseUrl() {
    if (!publicCourseHref || !navigator.clipboard?.writeText) {
      return;
    }

    await navigator.clipboard.writeText(
      `${window.location.origin}${publicCourseHref}`,
    );
    setShareLabel("Copied");
  }

  async function runLifecycleAction(
    action: "publish" | "unpublish" | "restore",
  ) {
    setActionError(null);
    setIsPending(true);
    try {
      const result =
        action === "publish"
          ? await publishCourse(courseRouteParam)
          : action === "restore"
            ? await restoreCourse(courseRouteParam)
            : await unpublishCourse(courseRouteParam);

      if (result.success) {
        setStatus(action === "publish" ? "published" : "draft");
        setShowUnpublishDialog(false);
        router.refresh();
      } else {
        setActionError(result.error);
      }
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The course lifecycle action failed.",
      );
    } finally {
      setIsPending(false);
    }
  }

  // Match the active segment after the dynamic course route param.
  const activeSegment = (() => {
    const marker = "/workspace/learning/courses/";
    const idx = pathname.indexOf(marker);
    if (idx === -1) return "";
    const tail = pathname.slice(idx + marker.length);
    return tail.split("/")[1] ?? "";
  })();

  return (
    <div className="flex min-w-0 max-w-full flex-col gap-6 overflow-x-hidden">
      {/* Header */}
      <div className="flex min-w-0 max-w-full flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-4">
          <Link
            href="/workspace/learning/courses"
            locale={locale}
            prefetch={false}
            className={cn(buttonVariants({ variant: "ghost", size: "icon" }))}
          >
            <ArrowLeft className="size-5" />
          </Link>
          <div className="flex size-12 shrink-0 items-center justify-center rounded-lg bg-linear-to-br from-emerald-500 to-teal-600">
            <BookOpen className="size-6 text-white" />
          </div>
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <h1 className="min-w-0 break-words text-2xl font-bold tracking-tight">
                {courseTitle}
              </h1>
              {getStatusBadge(status)}
            </div>
            <p className="max-w-prose break-words text-sm text-muted-foreground">
              {courseDescription}
            </p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2 sm:justify-end">
          {access.canEdit && (
            <Link
              href={previewHref}
              locale={locale}
              prefetch={false}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              <Eye className="mr-2 size-4" />
              Preview
            </Link>
          )}
          {access.canPublish && (
            <Button
              variant="outline"
              size="sm"
              type="button"
              disabled={!publicCourseHref}
              onClick={copyPublicCourseUrl}
            >
              <Share2 className="mr-2 size-4" />
              {shareLabel}
            </Button>
          )}
          {access.canPublish && (status === "published" ? (
            <Button
              variant="outline"
              size="sm"
              type="button"
              disabled={isPending}
              onClick={() => setShowUnpublishDialog(true)}
            >
              {isPending ? (
                <Loader2 className="mr-2 size-4 animate-spin" />
              ) : (
                <EyeOff className="mr-2 size-4" />
              )}
              Unpublish
            </Button>
          ) : status === "archived" ? (
            <Button
              variant="outline"
              size="sm"
              type="button"
              disabled={isPending}
              onClick={() => void runLifecycleAction("restore")}
            >
              {isPending ? (
                <Loader2 className="mr-2 size-4 animate-spin" />
              ) : (
                <Eye className="mr-2 size-4" />
              )}
              Restore
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              type="button"
              disabled={isPending}
              onClick={() => void runLifecycleAction("publish")}
            >
              {isPending ? (
                <Loader2 className="mr-2 size-4 animate-spin" />
              ) : (
                <Eye className="mr-2 size-4" />
              )}
              Publish
            </Button>
          ))}
          {access.canEdit && (
            <Link
              href={buildDashboardCoursePath(courseRouteParam, "settings")}
              locale={locale}
              prefetch={false}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              <Settings className="mr-2 size-4" />
              Settings
            </Link>
          )}
        </div>
      </div>

      {actionError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {actionError}
        </div>
      )}

      {/* Nav Tabs + Content */}
      <div className="flex min-w-0 flex-1 gap-6">
        {/* Sidebar Nav */}
        <nav className="hidden w-48 shrink-0 flex-col gap-1 lg:flex">
          {navItems.map((item) => {
            const isActive = activeSegment === item.segment;
            return (
              <Link
                key={item.segment}
                href={buildDashboardCoursePath(courseRouteParam, item.segment)}
                locale={locale}
                prefetch={false}
                className={`flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? "bg-muted text-foreground"
                    : "text-muted-foreground hover:bg-muted/50 hover:text-foreground"
                }`}
              >
                <item.icon className="size-4" />
                {item.title}
              </Link>
            );
          })}
        </nav>

        <div className="flex min-w-0 w-full flex-1 flex-col gap-6">
          {/* Mobile tabs - scrollable */}
          <div className="flex min-w-0 max-w-full gap-1 overflow-x-auto border-b pb-2 lg:hidden">
            {navItems.map((item) => {
              const isActive = activeSegment === item.segment;
              return (
                <Link
                  key={item.segment}
                  href={buildDashboardCoursePath(
                    courseRouteParam,
                    item.segment,
                  )}
                  locale={locale}
                  prefetch={false}
                  className={`flex shrink-0 items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                    isActive
                      ? "bg-muted text-foreground"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  <item.icon className="size-3.5" />
                  {item.title}
                </Link>
              );
            })}
          </div>
          <div className="min-w-0 flex-1">{children}</div>
        </div>
      </div>

      <Dialog open={showUnpublishDialog} onOpenChange={setShowUnpublishDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Unpublish this course?</DialogTitle>
            <DialogDescription>
              This will remove the course from the public catalog and block new
              enrollments. Existing students and course data are preserved.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="outline"
              type="button"
              onClick={() => setShowUnpublishDialog(false)}
              disabled={isPending}
            >
              Cancel
            </Button>
            <Button
              type="button"
              onClick={() => void runLifecycleAction("unpublish")}
              disabled={isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Unpublish course
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
