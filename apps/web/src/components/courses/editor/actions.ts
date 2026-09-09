'use server';

import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type LearningCoursesCreateProgram,
  type LearningCoursesProgram,
  type ProgramCategory,
  type LearningCoursesUpdateProgram,
} from '@game-guild/client';
import { revalidatePath } from 'next/cache';

import type { Course } from '@/lib/courses';
import { optionalPercentUnitsToPercentage, percentageToPercentUnits } from '@/lib/learning/academic-values';

interface EditorCourse extends Partial<Course> {
  id: string;
  title: string;
  slug: string;
  description: string;
  area?: string;
  level: 'Beginner' | 'Intermediate' | 'Advanced';
  difficulty?: number;
  thumbnail?: string;
  videoShowcaseUrl?: string;
  estimatedHours?: number;
  maxEnrollments?: number;
  enrollmentDeadline?: string;
  enrollmentStatus?: string;
  status?: string;
  tools?: string[];
  tags?: string[];
  instructors?: unknown[];
  isPublic?: boolean;
  isFeatured?: boolean;
  passingScore?: number;
  content?: unknown;
  createdAt?: string;
  updatedAt?: string;
}

const DEFAULT_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

function getApiClient() {
  return createServerClient({
    baseUrl: DEFAULT_API_URL.replace(/\/$/, ''),
    auth: { getAccessToken: () => getToken() },
  });
}

function createCourseModules() {
  const client = getApiClient();

  return {
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    lifecycle: new GeneratedApi.LearningCoursesProgramLifecycleModule(client),
  };
}

function normalizeSlug(slug: string): string {
  const normalizedSlug = slug.trim().toLowerCase();
  if (!normalizedSlug) {
    throw new Error('Empty slug provided');
  }

  return normalizedSlug;
}

function normalizeLevel(value: unknown): EditorCourse['level'] {
  if (value === 2 || value === '2') return 'Advanced';
  if (value === 1 || value === '1') return 'Intermediate';

  const text = typeof value === 'string' ? value.trim().toLowerCase() : '';
  if (text === 'advanced' || text === 'expert') return 'Advanced';
  if (text === 'intermediate') return 'Intermediate';
  return 'Beginner';
}

function toVisibility(isPublic: boolean | undefined, status: string | undefined): LearningCoursesUpdateProgram['visibility'] {
  if (isPublic === false) return 'Private';
  if (isPublic === true) return 'Public';
  return status?.toLowerCase() === 'published' ? 'Public' : undefined;
}

function joinList(values: string[] | undefined): string | undefined {
  return values && values.length > 0 ? values.join(', ') : undefined;
}

function toProgramCategory(value: string | undefined): ProgramCategory | undefined {
  const normalized = value?.replace(/[^a-zA-Z]/g, '').toLowerCase();
  switch (normalized) {
    case 'programming':
      return 'Programming';
    case 'datascience':
      return 'DataScience';
    case 'webdevelopment':
      return 'WebDevelopment';
    case 'mobiledevelopment':
      return 'MobileDevelopment';
    case 'gamedevelopment':
      return 'GameDevelopment';
    case 'ai':
      return 'AI';
    case 'cybersecurity':
      return 'Cybersecurity';
    case 'devops':
      return 'DevOps';
    case 'database':
      return 'Database';
    case 'business':
      return 'Business';
    case 'design':
      return 'Design';
    case 'marketing':
      return 'Marketing';
    case 'projectmanagement':
      return 'ProjectManagement';
    case 'personaldevelopment':
      return 'PersonalDevelopment';
    case 'creativearts':
      return 'CreativeArts';
    case 'science':
      return 'Science';
    case 'language':
      return 'Language';
    case 'other':
      return 'Other';
    default:
      return value ? 'General' : undefined;
  }
}

function toEnrollmentStatus(value: string | undefined): LearningCoursesUpdateProgram['enrollmentStatus'] {
  switch (value) {
    case 'closed':
      return 'Closed';
    case 'waitlist':
      return 'Waitlist';
    case 'invite-only':
      return 'InviteOnly';
    case 'open':
      return 'Open';
    default:
      return undefined;
  }
}

function toIsoDate(value: string | undefined): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

function mapCourse(program: LearningCoursesProgram): EditorCourse {
  const visibility = typeof program.visibility === 'string' ? program.visibility.toLowerCase() : '';

  return {
    id: program.id ?? '',
    title: program.title ?? '',
    slug: program.slug ?? '',
    description: program.description ?? '',
    area: typeof program.category === 'string' ? program.category : undefined,
    level: normalizeLevel(program.difficulty),
    status: typeof program.status === 'string' ? program.status : undefined,
    thumbnail: program.thumbnail ?? undefined,
    videoShowcaseUrl: program.videoShowcaseUrl ?? undefined,
    estimatedHours: program.estimatedHours ?? undefined,
    maxEnrollments: program.maxEnrollments ?? undefined,
    enrollmentDeadline: typeof program.enrollmentDeadline === 'string' ? program.enrollmentDeadline : undefined,
    enrollmentStatus: typeof program.enrollmentStatus === 'string' ? program.enrollmentStatus : undefined,
    isPublic: visibility === 'public',
    isFeatured: false,
    passingScore: optionalPercentUnitsToPercentage(program.passingScore) ?? undefined,
    tools: typeof program.skillsRequired === 'string'
      ? program.skillsRequired.split(',').map((tool) => tool.trim()).filter(Boolean)
      : [],
    tags: typeof program.skillsProvided === 'string'
      ? program.skillsProvided.split(',').map((tag) => tag.trim()).filter(Boolean)
      : [],
    instructors: [],
    createdAt: typeof program.createdAt === 'string' ? program.createdAt : undefined,
    updatedAt: typeof program.updatedAt === 'string' ? program.updatedAt : undefined,
    content: {
      chapters: [],
      syllabus: '',
      prerequisites: typeof program.skillsRequired === 'string' ? program.skillsRequired.split(',').map((item) => item.trim()).filter(Boolean) : [],
      objectives: typeof program.skillsProvided === 'string' ? program.skillsProvided.split(',').map((item) => item.trim()).filter(Boolean) : [],
      totalDuration: program.estimatedHours ? program.estimatedHours * 60 : 0,
      totalLessons: 0,
    },
  };
}

function toUpdateCourse(course: EditorCourse): LearningCoursesUpdateProgram {
  return {
    title: course.title.trim(),
    slug: course.slug.trim(),
    description: course.description.trim(),
    thumbnail: course.thumbnail?.trim() || undefined,
    videoShowcaseUrl: course.videoShowcaseUrl?.trim() || undefined,
    estimatedHours: course.estimatedHours,
    difficulty: course.level,
    category: toProgramCategory(course.area),
    visibility: toVisibility(course.isPublic, course.status),
    skillsProvided: joinList(course.tags),
    skillsRequired: joinList(course.tools),
    maxEnrollments: course.maxEnrollments,
    enrollmentDeadline: toIsoDate(course.enrollmentDeadline),
    enrollmentStatus: toEnrollmentStatus(course.enrollmentStatus),
    passingScore: course.passingScore == null ? undefined : percentageToPercentUnits(course.passingScore),
  };
}

export async function getCourseBySlug(slug: string): Promise<EditorCourse | null> {
  try {
    if (!slug) throw new Error('Invalid slug provided');

    const { programs } = createCourseModules();
    const result = await programs.getCoursesSlug(normalizeSlug(slug));

    return result.ok ? mapCourse(result.data) : null;
  } catch (error) {
    console.error('[getCourseBySlug] Error:', error);
    return null;
  }
}

export async function saveCourse(course: EditorCourse): Promise<boolean> {
  try {
    if (!course || typeof course !== 'object') throw new Error('Invalid course provided');
    if (!course.id) throw new Error('Course ID is required');

    const { programs } = createCourseModules();
    const result = await programs.putCourses(course.id, toUpdateCourse(course));

    if (!result.ok) return false;

    revalidatePath(`/workspace/learning/courses/${course.id}`);
    revalidatePath('/workspace/learning/courses');
    return true;
  } catch (error) {
    console.error('[saveCourse] Error:', error);
    return false;
  }
}

export async function autoSaveCourse(course: EditorCourse): Promise<boolean> {
  return saveCourse(course);
}

export async function createCourse(courseData: Partial<EditorCourse>): Promise<EditorCourse | null> {
  try {
    const title = courseData.title?.trim();
    const slug = courseData.slug?.trim();

    if (!title || title.length < 3) throw new Error('Title must be at least 3 characters');
    if (!slug) throw new Error('Slug is required');

    const body: LearningCoursesCreateProgram = {
      title,
      slug,
      description: courseData.description?.trim() ?? '',
      thumbnail: courseData.thumbnail?.trim() || undefined,
    };

    const { programs } = createCourseModules();
    const result = await programs.postCourses(body);

    if (!result.ok) return null;

    revalidatePath('/workspace/learning/courses');
    return mapCourse(result.data);
  } catch (error) {
    console.error('[createCourse] Error:', error);
    return null;
  }
}

export async function publishCourse(courseId: string): Promise<boolean> {
  try {
    if (!courseId) throw new Error('Course ID is required');

    const { lifecycle } = createCourseModules();
    const result = await lifecycle.postCoursesPublish(courseId);

    if (!result.ok) return false;

    revalidatePath(`/workspace/learning/courses/${courseId}`);
    revalidatePath('/workspace/learning/courses');
    return true;
  } catch (error) {
    console.error('[publishCourse] Error:', error);
    return false;
  }
}
