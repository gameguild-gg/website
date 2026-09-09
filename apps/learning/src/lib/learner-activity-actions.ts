'use server';

import { buildAssessmentPayload, buildContentActivityPayload, type LearnerContentActivityKind } from '@/lib/learner-activity-contracts';
import { createServerClient, GeneratedApi, type LearningAssessmentsSubmissionModality } from '@game-guild/client';
import { revalidatePath } from 'next/cache';

export interface LearnerMutationResult {
    success: boolean;
    error?: string;
}

function getApiUrl() {
    return process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
}

function errorMessage(error: unknown, fallback: string) {
    if (error && typeof error === 'object') {
        const value = error as { message?: unknown; title?: unknown; detail?: unknown };
        if (typeof value.detail === 'string' && value.detail.trim()) return value.detail;
        if (typeof value.message === 'string' && value.message.trim()) return value.message;
        if (typeof value.title === 'string' && value.title.trim()) return value.title;
    }
    return fallback;
}

function resolveSubmissionId(value: unknown): string | undefined {
    if (!value || typeof value !== 'object') return undefined;
    const response = value as Record<string, unknown>;
    if (typeof response.id === 'string' && response.id) return response.id;
    if (!response.submission || typeof response.submission !== 'object') return undefined;
    const submission = response.submission as Record<string, unknown>;
    return typeof submission.id === 'string' && submission.id ? submission.id : undefined;
}

async function authenticatedClient() {
    const { getToken } = await import('@/auth');
    const token = await getToken();
    if (!token) return null;
    return {
        token,
        client: createServerClient({ baseUrl: getApiUrl(), auth: { getAccessToken: async () => token } }),
    };
}

async function uploadAssessmentFile(token: string, assessmentId: string, file: File): Promise<string> {
    if (file.size === 0) throw new Error('Choose a file before submitting.');
    const upload = new FormData();
    upload.set('file', file, file.name);
    const endpoint = new URL('/v1/assets', getApiUrl());
    endpoint.searchParams.set('accessPolicy', 'Private');
    endpoint.searchParams.set('parentResourceType', 'AssessmentSubmission');
    endpoint.searchParams.set('parentResourceId', assessmentId);
    const response = await fetch(endpoint, { method: 'POST', headers: { Authorization: `Bearer ${token}` }, body: upload, cache: 'no-store' });
    const data = await response.json().catch(() => ({})) as { assetReferenceId?: string; title?: string; detail?: string };
    if (!response.ok || !data.assetReferenceId) throw new Error(data.detail || data.title || 'The file could not be uploaded.');
    return data.assetReferenceId;
}

export async function submitAssessment(formData: FormData): Promise<LearnerMutationResult> {
    try {
        const assessmentId = String(formData.get('assessmentId') || '');
        const enrollmentId = String(formData.get('enrollmentId') || '');
        const courseSlug = String(formData.get('courseSlug') || '');
        const modality = String(formData.get('modality') || 'Text') as LearningAssessmentsSubmissionModality;
        if (!assessmentId || !enrollmentId) return { success: false, error: 'Assessment enrollment context is missing.' };
        if (modality === 'None' || modality === 'StructuredAnswer') {
            return { success: false, error: 'Quiz attempts are unavailable through the generic assessment form.' };
        }

        const authenticated = await authenticatedClient();
        if (!authenticated) return { success: false, error: 'Your session expired. Sign in again.' };
        const assessments = new GeneratedApi.LearningAssessmentsModule(authenticated.client);
        const submissionsResult = await assessments.getAssessmentsMySubmissions(enrollmentId);
        const current = submissionsResult.ok
            ? submissionsResult.data.find((submission) => submission.assessmentId === assessmentId && submission.status === 'InProgress')
            : undefined;
        const started = current?.id
            ? current
            : await assessments.postAssessmentsSubmissionsStart(assessmentId, { enrollmentId }).then((result) => {
                if (!result.ok) throw new Error(`The assessment attempt could not be started: ${errorMessage(result.error, 'request failed')}`);
                return result.data;
            });
        const submissionId = resolveSubmissionId(started);
        if (!submissionId) throw new Error('The assessment attempt did not return a submission identifier.');

        const responseValue = modality === 'File'
            ? await uploadAssessmentFile(authenticated.token, assessmentId, formData.get('file') as File)
            : String(formData.get('response') || '');
        const payload = buildAssessmentPayload(modality, responseValue);
        const result = await assessments.postAssessmentsSubmissionsSubmit(submissionId, payload);
        if (!result.ok) return { success: false, error: `The assessment response could not be submitted: ${errorMessage(result.error, 'request failed')}` };

        if (courseSlug) {
            revalidatePath(`/courses/${courseSlug}/activities`);
            revalidatePath(`/courses/${courseSlug}/activities/assessment-${assessmentId}`);
        }
        revalidatePath('/grades');
        return { success: true };
    } catch (error) {
        return { success: false, error: errorMessage(error, 'The assessment could not be submitted.') };
    }
}

export async function submitContentActivity(formData: FormData): Promise<LearnerMutationResult> {
    try {
        const courseId = String(formData.get('courseId') || '');
        const enrollmentId = String(formData.get('enrollmentId') || '');
        const contentId = String(formData.get('contentId') || '');
        const courseSlug = String(formData.get('courseSlug') || '');
        const kind = String(formData.get('kind') || '') as LearnerContentActivityKind;
        const response = String(formData.get('response') || '');
        if (!courseId || !enrollmentId || !contentId || !['discussion', 'reflection', 'survey'].includes(kind)) {
            return { success: false, error: 'Activity enrollment context is missing.' };
        }

        const authenticated = await authenticatedClient();
        if (!authenticated) return { success: false, error: 'Your session expired. Sign in again.' };
        const interactions = new GeneratedApi.LearningCoursesContentInteractionModule(authenticated.client);
        let interaction = await interactions.getCourseInteractionsUserContent(enrollmentId, contentId, { programId: courseId });
        if (!interaction.ok) {
            interaction = await interactions.postCourseInteractions({ contentId, programUserId: enrollmentId }, { programId: courseId });
        }
        if (!interaction.ok || !interaction.data.id) return { success: false, error: 'The activity could not be started.' };
        const payload = JSON.stringify(buildContentActivityPayload(kind, response));
        const result = await interactions.postCourseInteractionsSubmit(
            interaction.data.id,
            { contentId, programUserId: enrollmentId, submissionData: payload },
            { programId: courseId },
        );
        if (!result.ok) return { success: false, error: errorMessage(result.error, 'The activity response could not be submitted.') };

        if (courseSlug) {
            revalidatePath(`/courses/${courseSlug}/activities`);
            revalidatePath(`/courses/${courseSlug}/activities/content-${contentId}`);
            revalidatePath(`/courses/${courseSlug}/content`);
        }
        return { success: true };
    } catch (error) {
        return { success: false, error: errorMessage(error, 'The activity response could not be submitted.') };
    }
}

export async function createCourseDiscussion(formData: FormData): Promise<LearnerMutationResult> {
    try {
        const courseId = String(formData.get('courseId') || '');
        const courseSlug = String(formData.get('courseSlug') || '');
        const title = String(formData.get('title') || '').trim();
        const content = String(formData.get('content') || '').trim();
        if (!courseId || !title || !content) return { success: false, error: 'Title and message are required.' };
        const authenticated = await authenticatedClient();
        if (!authenticated) return { success: false, error: 'Your session expired. Sign in again.' };
        const discussions = new GeneratedApi.LearningExperienceSocialDiscussionsModule(authenticated.client);
        const result = await discussions.postApiSocialDiscussions({ courseId, title, content });
        if (!result.ok) return { success: false, error: errorMessage(result.error, 'The discussion could not be created.') };
        revalidatePath(`/courses/${courseSlug}/community`);
        return { success: true };
    } catch (error) {
        return { success: false, error: errorMessage(error, 'The discussion could not be created.') };
    }
}
