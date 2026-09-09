'use server';

/**
 * Server actions for the courses module.
 */

import { auth, getToken } from '@/auth';
import {
    createServerClient,
    GeneratedApi,
    type LearningCoursesContentProgress,
    type LearningCoursesProgramContent,
    type LearningCoursesProgressStatus,
} from '@game-guild/client';
import {
    parseQuizContentDocument,
    prepareQuizContentForRuntime,
} from '@game-guild/quiz-content';
import {
    percentageToPercentUnits,
    percentUnitsToPercentage,
} from '@/lib/learning/academic-values';

interface SubmitActivityData {
    activityId: string;
    courseId?: string;
    activityType: string;
    content: unknown;
    isGraded: boolean;
    attempt: number;
    submissionData?: unknown;
}

interface SubmitActivityResult {
    success: boolean;
    message?: string;
    score?: number;
    feedback?: string;
    submission?: {
        id: string;
        status: string;
    };
}

const UUID_PATTERN =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function isUuid(value: string | null | undefined): value is string {
    return typeof value === 'string' && UUID_PATTERN.test(value);
}

function buildSubmissionData(data: SubmitActivityData): string {
    return JSON.stringify({
        activityType: data.activityType,
        attempt: data.attempt,
        isGraded: data.isGraded,
        content: data.content,
        submissionData: data.submissionData ?? null,
    });
}

async function resolveProgramId(
    courseId: string,
    programs: InstanceType<typeof GeneratedApi.LearningCoursesProgramModule>
): Promise<string | null> {
    if (isUuid(courseId)) {
        return courseId;
    }

    const courseResult = await programs.getCoursesSlug(courseId);
    if (!courseResult.ok || !isUuid(courseResult.data.id)) {
        return null;
    }

    return courseResult.data.id;
}

function getApiErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'message' in error && typeof error.message === 'string') {
        return error.message;
    }

    return 'Failed to submit activity.';
}

export async function submitActivity(
    data: SubmitActivityData
): Promise<SubmitActivityResult> {
    try {
        if (data.activityType === 'quiz' && data.isGraded) {
            return {
                success: false,
                message: 'Official graded submissions are not available through the generic activity endpoint.',
            };
        }

        const userId = await getCurrentUserId();
        if (!userId) {
            return {
                success: false,
                message: 'You must be signed in to submit this activity.',
            };
        }

        if (!data.courseId || !isUuid(data.activityId)) {
            return {
                success: false,
                message: 'This activity is not backed by publishable course content yet.',
            };
        }

        const { programs, content } = createCourseModules();
        const programId = await resolveProgramId(data.courseId, programs);

        if (!programId) {
            return {
                success: false,
                message: 'Unable to resolve the course for this activity submission.',
            };
        }

        const result = await content.postCoursesContentSubmit(programId, data.activityId, {
            submissionData: buildSubmissionData(data),
        });

        if (!result.ok) {
            return {
                success: false,
                message: getApiErrorMessage(result.error),
            };
        }

        return {
            success: true,
            message: 'Activity submitted successfully.',
            submission: {
                id: result.data.id ?? data.activityId,
                status: result.data.submittedAt ? 'submitted' : (result.data.status ?? 'submitted'),
            },
        };
    } catch (error) {
        console.error('[submitActivity] Failed to submit activity', error);

        return {
            success: false,
            message: getApiErrorMessage(error),
        };
    }
}

function getApiClient() {
    const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

    return createServerClient({
        baseUrl: apiUrl,
        auth: { getAccessToken: () => getToken() },
    });
}

function createCourseModules() {
    const client = getApiClient();

    return {
        programs: new GeneratedApi.LearningCoursesProgramModule(client),
        content: new GeneratedApi.LearningCoursesProgramContentModule(client),
    };
}

async function getCurrentUserId(): Promise<string | null> {
    const session = await auth();
    return session?.user?.id ?? null;
}

function flattenContent(items: LearningCoursesProgramContent[]): LearningCoursesProgramContent[] {
    const flattened: LearningCoursesProgramContent[] = [];

    const visit = (item: LearningCoursesProgramContent) => {
        flattened.push(item);

        for (const child of item.children ?? []) {
            visit(child);
        }
    };

    for (const item of items) {
        visit(item);
    }

    return flattened.sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0));
}

function mapProgressItemType(type: LearningCoursesProgramContent['type']): CourseProgress['items'][number]['type'] {
    switch (type) {
        case 'Assignment':
            return 'assignment';
        case 'Questionnaire':
            return 'quiz';
        case 'Discussion':
            return 'peer-review';
        case 'Code':
        case 'Project':
        case 'Reflection':
        case 'Survey':
            return 'activity';
        case 'Lesson':
        default:
            return 'lesson';
    }
}

function mapProgressStatus(status?: LearningCoursesProgressStatus): CourseProgress['items'][number]['status'] {
    switch (status) {
        case 'Completed':
            return 'completed';
        case 'Submitted':
            return 'graded';
        case 'InProgress':
            return 'in-progress';
        case 'NotStarted':
        default:
            return 'not-started';
    }
}

function getContentProgressMap(contentProgress: LearningCoursesContentProgress[] | null | undefined) {
    return new Map(
        (contentProgress ?? [])
            .filter((entry): entry is LearningCoursesContentProgress & { contentId: string } => Boolean(entry.contentId))
            .map((entry) => [entry.contentId, entry]),
    );
}

interface CourseProgress {
    courseId: string;
    courseTitle: string;
    totalItems: number;
    completedItems: number;
    percentComplete: number;
    progressPercentage: number;
    currentStreak: number;
    timeSpent: number;
    items: Array<{
        id: string;
        title: string;
        type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
        status: 'not-started' | 'in-progress' | 'completed' | 'graded';
        completedAt?: string;
        grade?: number;
        required: boolean;
        estimatedMinutes?: number;
    }>;
    nextItem?: {
        id: string;
        title: string;
        type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
        status: 'not-started' | 'in-progress' | 'completed' | 'graded';
        completedAt?: string;
        grade?: number;
        required: boolean;
        estimatedMinutes?: number;
    };
    estimatedTimeToComplete: number;
    certificateEligible: boolean;
    lastAccessedAt?: string;
    modules: Array<{
        id: string;
        title: string;
        progress: number;
    }>;
}

export interface CourseLearningItem {
    id: string;
    title: string;
    type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
    status: 'locked' | 'available' | 'in-progress' | 'completed';
    duration?: number;
    description?: string;
    order: number;
    isRequired: boolean;
    activityType?: 'text' | 'code' | 'file' | 'quiz' | 'discussion';
    content?: unknown;
    progress?: number;
    score?: number;
    maxScore?: number;
}

export interface CourseLearningModule {
    id: string;
    title: string;
    description: string;
    order: number;
    items: CourseLearningItem[];
    isLocked: boolean;
    progress: number;
}

export interface CourseLearningData {
    id: string;
    title: string;
    description: string;
    modules: CourseLearningModule[];
    overallProgress: number;
    totalItems: number;
    completedItems: number;
    currentItem?: CourseLearningItem;
    estimatedTimeToComplete: number;
}

interface CourseLearningModuleSource {
    id: string;
    title: string;
    description: string;
    order: number;
    items: LearningCoursesProgramContent[];
}

function mapLearningActivityType(type: LearningCoursesProgramContent['type']): CourseLearningItem['activityType'] {
    switch (type) {
        case 'Code':
            return 'code';
        case 'Assignment':
            return 'file';
        case 'Questionnaire':
            return 'quiz';
        case 'Discussion':
            return 'discussion';
        case 'Project':
        case 'Reflection':
        case 'Survey':
        case 'Lesson':
        default:
            return 'text';
    }
}

function mapLearningItemType(type: LearningCoursesProgramContent['type']): CourseLearningItem['type'] {
    switch (type) {
        case 'Assignment':
            return 'assignment';
        case 'Questionnaire':
            return 'quiz';
        case 'Discussion':
            return 'peer-review';
        case 'Code':
        case 'Project':
        case 'Reflection':
        case 'Survey':
            return 'activity';
        case 'Lesson':
        default:
            return 'lesson';
    }
}

function mapLearningStatus(
    progressStatus: CourseProgress['items'][number]['status'] | undefined,
    unlocked: boolean
): CourseLearningItem['status'] {
    if (progressStatus === 'completed' || progressStatus === 'graded') {
        return 'completed';
    }

    if (progressStatus === 'in-progress') {
        return 'in-progress';
    }

    return unlocked ? 'available' : 'locked';
}

function prepareQuizContentForLearner(
    contentBody: Record<string, unknown> | null | undefined,
): ReturnType<typeof prepareQuizContentForRuntime> | undefined {
    if (!contentBody) return undefined;

    const { document } = parseQuizContentDocument(contentBody);
    const runtime = prepareQuizContentForRuntime(
        document,
        document.grading ? 'server-graded' : 'local-practice',
    );
    return runtime;
}

export async function getCourseLearningData(courseSlug: string): Promise<CourseLearningData | null> {
    try {
        const { programs, content } = createCourseModules();
        const courseResult = await programs.getCoursesSlug(courseSlug);

        if (!courseResult.ok || !courseResult.data.id) {
            return null;
        }

        const courseId = courseResult.data.id;
        const [contentResult, progress] = await Promise.all([content.getCoursesContent(courseId), getCourseProgress(courseId)]);

        if (!contentResult.ok) {
            return {
                id: courseId,
                title: courseResult.data.title ?? 'Course',
                description: courseResult.data.description ?? '',
                modules: [],
                overallProgress: progress.progressPercentage,
                totalItems: 0,
                completedItems: 0,
                estimatedTimeToComplete: progress.estimatedTimeToComplete,
            };
        }

        const flatContent = flattenContent(contentResult.data).filter((item) => item.id);
        const progressById = new Map(progress.items.map((item) => [item.id, item]));
        const topLevelContent = flatContent.filter((item) => !item.parentId).sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0));
        const hasNestedContent = flatContent.some((item) => Boolean(item.parentId));
        const moduleSources: CourseLearningModuleSource[] = hasNestedContent
            ? topLevelContent.map((module) => ({
                id: module.id!,
                title: module.title ?? 'Untitled module',
                description: module.description ?? '',
                order: module.sortOrder ?? 0,
                items: flatContent.filter((item) => item.parentId === module.id).sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0)),
            }))
            : [
                {
                    id: `${courseId}-content`,
                    title: 'Course Content',
                    description: courseResult.data.description ?? '',
                    order: 0,
                    items: topLevelContent,
                },
            ];
        let nextUnlockedAssigned = false;

        const mappedModules = moduleSources.map((module) => {
            const moduleItems = module.items.map((item) => {
                const itemProgress = progressById.get(item.id ?? '');
                const unlocked = !nextUnlockedAssigned;
                const status = mapLearningStatus(itemProgress?.status, unlocked);

                if (status === 'available' || status === 'in-progress') {
                    nextUnlockedAssigned = true;
                }

                return {
                    id: item.id!,
                    title: item.title ?? 'Untitled content',
                    type: mapLearningItemType(item.type),
                    status,
                    duration: item.estimatedMinutes ?? undefined,
                    description: item.description ?? undefined,
                    order: item.sortOrder ?? 0,
                    isRequired: item.isRequired ?? false,
                    activityType: mapLearningActivityType(item.type),
                    content: item.type === 'Questionnaire'
                        ? prepareQuizContentForLearner(item.jsonBody)
                        : item.body ?? undefined,
                    progress: itemProgress?.status === 'completed' || itemProgress?.status === 'graded' ? 100 : itemProgress?.status === 'in-progress' ? 50 : 0,
                } satisfies CourseLearningItem;
            });

            const completedItems = moduleItems.filter((item) => item.status === 'completed').length;

            return {
                id: module.id,
                title: module.title,
                description: module.description,
                order: module.order,
                items: moduleItems,
                isLocked: moduleItems.length > 0 && moduleItems.every((item) => item.status === 'locked'),
                progress: moduleItems.length > 0 ? Math.round((completedItems / moduleItems.length) * 100) : 0,
            } satisfies CourseLearningModule;
        });

        const currentItem = mappedModules
            .flatMap((module) => module.items)
            .find((item) => item.status === 'in-progress' || item.status === 'available');

        return {
            id: courseId,
            title: courseResult.data.title ?? 'Course',
            description: courseResult.data.description ?? '',
            modules: mappedModules,
            overallProgress: progress.progressPercentage,
            totalItems: progress.totalItems,
            completedItems: progress.completedItems,
            currentItem,
            estimatedTimeToComplete: progress.estimatedTimeToComplete,
        };
    } catch (error) {
        console.error('[getCourseLearningData] Failed to load course learning data', error);
        return null;
    }
}

export async function getCourseProgress(_courseId: string): Promise<CourseProgress> {
    const empty: CourseProgress = {
        courseId: _courseId,
        courseTitle: 'Course',
        totalItems: 0,
        completedItems: 0,
        percentComplete: 0,
        progressPercentage: 0,
        currentStreak: 0,
        timeSpent: 0,
        items: [],
        estimatedTimeToComplete: 0,
        certificateEligible: false,
        modules: [],
    };

    try {
        const { programs, content } = createCourseModules();
        const userId = await getCurrentUserId();
        const [courseResult, contentResult, progressResult] = await Promise.all([
            programs.getCoursesForGetCoursesById(_courseId),
            content.getCoursesContent(_courseId),
            userId ? programs.getCoursesMeProgress(_courseId) : Promise.resolve(undefined),
        ]);

        const courseTitle = courseResult.ok ? (courseResult.data.title ?? 'Course') : empty.courseTitle;
        const flatContent = contentResult.ok ? flattenContent(contentResult.data) : [];
        const progress = progressResult && progressResult.ok ? progressResult.data : undefined;
        const progressByContentId = getContentProgressMap(progress?.contentProgress);

        const items = flatContent.map((item) => {
            const itemProgress = progressByContentId.get(item.id ?? '');
            return {
                id: item.id ?? crypto.randomUUID(),
                title: item.title ?? 'Untitled content',
                type: mapProgressItemType(item.type),
                status: mapProgressStatus(itemProgress?.status),
                completedAt: itemProgress?.completedAt ?? undefined,
                required: item.isRequired ?? false,
                estimatedMinutes: item.estimatedMinutes ?? undefined,
            };
        });

        const completedItems = items.filter((item) => item.status === 'completed' || item.status === 'graded').length;
        const progressPercentage = Math.round(
            progress?.completionPercentage == null
                ? (items.length > 0 ? (completedItems / items.length) * 100 : 0)
                : percentUnitsToPercentage(progress.completionPercentage),
        );
        const timeSpent = items
            .filter((item) => item.status === 'completed' || item.status === 'graded')
            .reduce((total, item) => total + (item.estimatedMinutes ?? 0), 0);
        const remainingMinutes = items
            .filter((item) => item.status !== 'completed' && item.status !== 'graded')
            .reduce((total, item) => total + (item.estimatedMinutes ?? 0), 0);

        return {
            courseId: _courseId,
            courseTitle,
            totalItems: items.length,
            completedItems,
            percentComplete: progressPercentage,
            progressPercentage,
            currentStreak: 0,
            timeSpent,
            items,
            nextItem: items.find((item) => item.status !== 'completed' && item.status !== 'graded'),
            estimatedTimeToComplete: Math.ceil(remainingMinutes / 60),
            certificateEligible: Boolean(progress?.completedAt) || (items.length > 0 && completedItems === items.length),
            lastAccessedAt: progress?.lastAccessedAt ?? undefined,
            modules: [],
        };
    } catch (error) {
        console.error('[getCourseProgress] Failed to load course progress', error);
        return empty;
    }
}

export async function updateCourseProgress(
    _courseId: string,
    _contentId: string,
    _progress: number
): Promise<{ success: boolean }> {
    try {
        const { programs } = createCourseModules();
        const status: LearningCoursesProgressStatus = _progress >= 100
            ? 'Completed'
            : _progress > 0
                ? 'InProgress'
                : 'NotStarted';

        const result = await programs.putCoursesMeProgress(_courseId, {
            status,
            lastAccessedAt: new Date().toISOString(),
            additionalData: {
                content: {
                    contentId: _contentId,
                    completionPercentage: percentageToPercentUnits(_progress),
                },
            },
        });

        return { success: result.ok };
    } catch (error) {
        console.error('[updateCourseProgress] Failed to update progress', error);
        return { success: false };
    }
}

export async function markContentComplete(
    _courseId: string,
    _contentId: string
): Promise<{ success: boolean }> {
    try {
        const { programs } = createCourseModules();
        const result = await programs.postCoursesMeContentComplete(_courseId, _contentId);

        return { success: result.ok };
    } catch (error) {
        console.error('[markContentComplete] Failed to mark content complete', error);
        return { success: false };
    }
}
