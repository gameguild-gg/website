'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card';
import type { GeneratedApi } from '@game-guild/client';
import { Archive, BookOpen, Clock, Eye, FileText, Play, Star, Users } from 'lucide-react';

type LearningCourse = GeneratedApi.LearningCoursesProgram;
type CourseProgramContent = GeneratedApi.LearningCoursesProgramContent | { id?: string | number; title?: string | null };

type CourseAnalytics = {
  enrollments?: number;
  averageRating?: number;
};

type Course = Omit<
  Partial<LearningCourse>,
  'id' | 'title' | 'slug' | 'description' | 'status' | 'difficulty' | 'category' | 'estimatedHours' | 'currentEnrollments' | 'averageRating' | 'totalRatings'
> & {
  id?: string | number;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  area?: string;
  status?: 'draft' | 'published' | 'archived' | string | number | null;
  level?: number | null;
  difficulty?: string | number | null;
  category?: string | number | null;
  estimatedHours?: number | null;
  currentEnrollments?: number | null;
  averageRating?: number | null;
  totalRatings?: number | null;
  programContents?: CourseProgramContent[] | null;
  analytics?: CourseAnalytics;
  tools?: string[];
  content?: unknown[];
};

export type CourseCardCourse = Course;

interface CourseCardProps {
  course: Course;
  onEdit?: (course: Course) => void;
  onView?: (course: Course) => void;
  onEnroll?: (course: Course) => void;
}

function normalizeStatusValue(status: Course['status']): string {
  if (typeof status === 'number') {
    if (status === 0) return 'draft';
    if (status === 1) return 'review';
    if (status === 2) return 'published';
    if (status === 3) return 'archived';
  }

  const normalized = typeof status === 'string' ? status.trim().toLowerCase() : '';
  if (normalized === 'underreview' || normalized === 'under-review' || normalized === 'review') return 'review';
  if (normalized === 'published') return 'published';
  if (normalized === 'archived') return 'archived';
  return 'draft';
}

function normalizeDifficultyLevel(course: Course): number {
  if (typeof course.level === 'number') return course.level;
  if (typeof course.difficulty === 'number') return course.difficulty + 1;

  const normalized = typeof course.difficulty === 'string' ? course.difficulty.trim().toLowerCase() : '';
  if (normalized === 'intermediate') return 2;
  if (normalized === 'advanced') return 3;
  if (normalized === 'expert') return 4;
  return 1;
}

export function CourseCard({ course, onEdit, onView, onEnroll }: CourseCardProps) {
  const normalizedStatus = normalizeStatusValue(course.status);
  const normalizedLevel = normalizeDifficultyLevel(course);
  const normalizedArea = course.area ?? course.category;
  const lessonsCount = (course.content ?? course.programContents)?.length || 0;
  const analyticsEnrollments = course.analytics?.enrollments ?? course.currentEnrollments ?? 0;
  const analyticsRating = course.analytics?.averageRating ?? (typeof course.averageRating === 'number' ? course.averageRating : undefined);
  const title = course.title || 'Untitled course';

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'published':
        return 'bg-green-500/20 text-green-400 border-green-500/30';
      case 'draft':
        return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30';
      case 'archived':
        return 'bg-red-500/20 text-red-400 border-red-500/30';
      default:
        return 'bg-slate-500/20 text-slate-400 border-slate-500/30';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'published':
        return <Eye className="h-3 w-3" />;
      case 'draft':
        return <FileText className="h-3 w-3" />;
      case 'archived':
        return <Archive className="h-3 w-3" />;
      default:
        return <FileText className="h-3 w-3" />;
    }
  };

  const getLevelColor = (level: number) => {
    switch (level) {
      case 1:
        return 'text-green-400';
      case 2:
        return 'text-yellow-400';
      case 3:
        return 'text-orange-400';
      case 4:
        return 'text-red-400';
      default:
        return 'text-slate-400';
    }
  };

  const getLevelName = (level: number) => {
    switch (level) {
      case 1:
        return 'Beginner';
      case 2:
        return 'Intermediate';
      case 3:
        return 'Advanced';
      case 4:
        return 'Expert';
      default:
        return 'Unknown';
    }
  };

  return (
    <Card className="group relative backdrop-blur-md bg-slate-900/40 border border-slate-700/50 hover:border-blue-500/50 transition-all duration-300 hover:shadow-xl hover:shadow-blue-500/10">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <h3 className="font-semibold text-white text-lg leading-tight line-clamp-2 group-hover:text-blue-300 transition-colors">{title}</h3>
            <p className="text-slate-400 text-sm mt-1 capitalize">{normalizedArea || 'General'}</p>
          </div>
          <div className="flex flex-col gap-2 items-end shrink-0">
            <Badge className={`text-xs px-2 py-1 ${getStatusColor(normalizedStatus)} flex items-center gap-1`}>
              {getStatusIcon(normalizedStatus)}
              {normalizedStatus}
            </Badge>
            <div className={`text-xs font-medium ${getLevelColor(normalizedLevel)}`}>
              Level {normalizedLevel} • {getLevelName(normalizedLevel)}
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent className="pb-4">
        <p className="text-slate-300 text-sm line-clamp-3 mb-4">{course.description || ''}</p>

        <div className="flex items-center gap-4 text-sm text-slate-400">
          <div className="flex items-center gap-1">
            <BookOpen className="h-4 w-4" />
            <span>{lessonsCount} lessons</span>
          </div>
          <div className="flex items-center gap-1">
            <Clock className="h-4 w-4" />
            <span>{course.estimatedHours || 0}h</span>
          </div>
          <div className="flex items-center gap-1">
            <Users className="h-4 w-4" />
            <span>{analyticsEnrollments}</span>
          </div>
          {analyticsRating !== undefined && (
            <div className="flex items-center gap-1">
              <Star className="h-4 w-4 text-yellow-400" />
              <span>{analyticsRating.toFixed(1)}</span>
            </div>
          )}
        </div>

        {course.tools && course.tools.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-1">
            {course.tools.slice(0, 3).map((tool: string) => (
              <Badge key={tool} variant="outline" className="text-xs px-2 py-0.5 bg-slate-800/50 text-slate-300 border-slate-600">
                {tool}
              </Badge>
            ))}
            {course.tools.length > 3 && (
              <Badge variant="outline" className="text-xs px-2 py-0.5 bg-slate-800/50 text-slate-300 border-slate-600">
                +{course.tools.length - 3} more
              </Badge>
            )}
          </div>
        )}
      </CardContent>

      <CardFooter className="pt-2">
        <div className="flex gap-2 w-full">
          {onView && (
            <Button size="sm" variant="outline" onClick={() => onView(course)} className="flex-1">
              <Eye className="h-4 w-4 mr-1" />
              View
            </Button>
          )}
          {onEdit && (
            <Button size="sm" variant="outline" onClick={() => onEdit(course)} className="flex-1">
              <FileText className="h-4 w-4 mr-1" />
              Edit
            </Button>
          )}
          {onEnroll && normalizedStatus === 'published' && (
            <Button size="sm" onClick={() => onEnroll(course)} className="flex-1">
              <Play className="h-4 w-4 mr-1" />
              Enroll
            </Button>
          )}
        </div>
      </CardFooter>
    </Card>
  );
}
