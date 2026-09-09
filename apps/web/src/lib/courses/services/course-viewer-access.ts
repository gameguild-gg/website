import 'server-only';

import { auth, getToken } from '@/auth';
import { createServerClient, GeneratedApi, type ApiError } from '@game-guild/client';
import { percentUnitsToPercentage } from '@/lib/learning/academic-values';

export type CourseViewerAccessState = 'signed-out' | 'has-access' | 'no-access' | 'unavailable';

export interface CourseViewerAccess {
    state: CourseViewerAccessState;
    progressPercentage?: number;
    lastAccessedAt?: string | null;
    error?: string;
}

const DEFAULT_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

function getApiUrl(): string {
    return DEFAULT_API_URL.replace(/\/$/, '');
}

function createAuthenticatedProgramsModule(accessToken: string) {
    const client = createServerClient({
        baseUrl: getApiUrl(),
        auth: { getAccessToken: async () => accessToken },
    });

    return new GeneratedApi.LearningCoursesProgramModule(client);
}

function formatApiError(error: ApiError | undefined): string {
    if (!error) {
        return 'Unknown error';
    }

    const status = typeof error.status === 'number' ? error.status : 'unknown';
    const detail = 'detail' in error && typeof error.detail === 'string' ? error.detail : undefined;

    return `[${status}] ${detail || error.message || 'Request failed'}`;
}

export async function getCourseViewerAccess(courseId: string): Promise<CourseViewerAccess> {
    const accessToken = await getToken();

    if (!accessToken) {
        return { state: 'signed-out' };
    }

    const session = await auth();

    if (!session?.user?.id) {
        return { state: 'signed-out' };
    }

    try {
        const programs = createAuthenticatedProgramsModule(accessToken);
        const progressResult = await programs.getCoursesMeProgress(courseId);

        if (progressResult.ok) {
            return {
                state: 'has-access',
                progressPercentage: percentUnitsToPercentage(
                    progressResult.data.completionPercentage ?? 0,
                ),
                lastAccessedAt: progressResult.data.lastAccessedAt ?? null,
            };
        }

        if (progressResult.error?.status === 401) {
            return { state: 'signed-out' };
        }

        if (progressResult.error?.status === 403 || progressResult.error?.status === 404) {
            return { state: 'no-access' };
        }

        return {
            state: 'unavailable',
            error: formatApiError(progressResult.error),
        };
    } catch (error) {
        return {
            state: 'unavailable',
            error: error instanceof Error ? error.message : 'Unknown error',
        };
    }
}
