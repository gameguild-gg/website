'use server';

import { createServerClient, GeneratedApi, type LearningCoursesContentInteractionEventType } from '@game-guild/client';
import { percentageToPercentUnits } from '@/lib/learning/academic-values';

export interface LessonEventInput {
    courseId: string;
    enrollmentId: string;
    contentId: string;
    type: LearningCoursesContentInteractionEventType;
    positionSeconds?: number;
    durationSeconds?: number;
    progressPercentage?: number;
    idempotencyKey: string;
}

export async function recordLessonEvent(input: LessonEventInput): Promise<{ success: true } | { success: false; error: string }> {
    try {
        const { getToken } = await import('@/auth');
        const token = await getToken();
        if (!token) return { success: false, error: 'Your session expired.' };
        const client = createServerClient({ baseUrl: process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080', auth: { getAccessToken: async () => token } });
        const interactions = new GeneratedApi.LearningCoursesContentInteractionModule(client);
        let interactionResult = await interactions.getCourseInteractionsUserContent(input.enrollmentId, input.contentId, { programId: input.courseId });
        if (!interactionResult.ok) {
            interactionResult = await interactions.postCourseInteractions({ contentId: input.contentId, programUserId: input.enrollmentId }, { programId: input.courseId });
        }
        if (!interactionResult.ok || !interactionResult.data.id) return { success: false, error: 'Unable to start lesson tracking.' };
        const events = new GeneratedApi.LearningCoursesLessonInteractionEventsModule(client);
        const eventResult = await events.postCoursesInteractionsEvents(input.courseId, interactionResult.data.id, {
            type: input.type,
            occurredAt: new Date().toISOString(),
            positionSeconds: input.positionSeconds,
            durationSeconds: input.durationSeconds,
            progressPercentage: input.progressPercentage == null
                ? undefined
                : percentageToPercentUnits(input.progressPercentage),
            idempotencyKey: input.idempotencyKey,
        });
        return eventResult.ok ? { success: true } : { success: false, error: 'Unable to record lesson progress.' };
    } catch (error) {
        return { success: false, error: error instanceof Error ? error.message : 'Unable to record lesson progress.' };
    }
}
