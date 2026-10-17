'use server';

/**
 * Server actions and queries for the public course-proposal pipeline (issue #105).
 *
 * Uses plain fetch against the public API endpoints (same pattern as
 * lib/contact/actions.ts) so the page does not depend on session state.
 */

export interface PublicCourseProposal {
    id: string;
    slug: string;
    status: string;
    courseTitle: string;
    pitch: string | null;
    audience: string | null;
    proposerName: string | null;
    metadata: string | null;
    publishedAt: string | null;
}

type ActionResult = { success: true; message: string } | { success: false; error: string };

function getApiUrl(): string {
    return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080').replace(/\/$/, '');
}

function getFirstProblemDetailError(payload: unknown): string | null {
    if (!payload || typeof payload !== 'object') {
        return null;
    }

    const candidate = payload as {
        title?: unknown;
        detail?: unknown;
        error?: unknown;
        errors?: Record<string, unknown>;
    };

    if (candidate.errors && typeof candidate.errors === 'object') {
        for (const value of Object.values(candidate.errors)) {
            if (Array.isArray(value) && value.length > 0 && typeof value[0] === 'string') {
                return value[0];
            }
        }
    }

    if (typeof candidate.detail === 'string' && candidate.detail.trim().length > 0) {
        return candidate.detail;
    }

    if (typeof candidate.title === 'string' && candidate.title.trim().length > 0) {
        return candidate.title;
    }

    if (typeof candidate.error === 'string' && candidate.error.trim().length > 0) {
        return candidate.error;
    }

    return null;
}

/** Loads the published proposal for the public detail page; null when absent or unpublished. */
export async function getPublishedProposal(slug: string): Promise<PublicCourseProposal | null> {
    const normalized = slug.trim().toLowerCase();
    if (!normalized) return null;

    try {
        const response = await fetch(
            `${getApiUrl()}/v1/content/course-proposals/by-slug/${encodeURIComponent(normalized)}`,
            { cache: 'no-store' },
        );
        if (!response.ok) {
            return null;
        }
        return (await response.json()) as PublicCourseProposal;
    } catch {
        return null;
    }
}

export interface ProposalInterestInput {
    name: string;
    email: string;
    message: string;
    pagePath?: string;
    locale?: string;
}

/** Registers interest in a published proposal via the marketing-lead pipeline. */
export async function submitProposalInterestAction(input: ProposalInterestInput): Promise<ActionResult> {
    if (!input.name?.trim()) {
        return { success: false, error: 'Name is required.' };
    }

    if (!input.email?.trim()) {
        return { success: false, error: 'Email is required.' };
    }

    if (!input.message?.trim() || input.message.trim().length < 10) {
        return { success: false, error: 'Message must be at least 10 characters.' };
    }

    try {
        const response = await fetch(`${getApiUrl()}/v1/marketing/leads`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                source: 'course-proposal',
                name: input.name.trim(),
                email: input.email.trim(),
                topic: 'partnership',
                message: input.message.trim(),
                pagePath: input.pagePath,
                locale: input.locale,
            }),
            cache: 'no-store',
        });

        if (!response.ok) {
            let payload: unknown = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }
            return {
                success: false,
                error: getFirstProblemDetailError(payload) || 'Failed to register your interest. Please try again.',
            };
        }

        return { success: true, message: 'Thanks! Your interest has been recorded.' };
    } catch (error) {
        return {
            success: false,
            error: error instanceof Error ? error.message : 'Failed to register your interest. Please try again.',
        };
    }
}

export interface CourseProposalSubmissionInput {
    courseTitle: string;
    pitch: string;
    audience?: string;
    proposerName: string;
    proposerEmail: string;
    metadata?: string;
}

/** Submits a new course proposal through the public creator pipeline. */
export async function submitCourseProposalAction(input: CourseProposalSubmissionInput): Promise<ActionResult> {
    if (!input.courseTitle?.trim() || input.courseTitle.trim().length < 4) {
        return { success: false, error: 'Course title must be at least 4 characters.' };
    }

    if (!input.proposerName?.trim()) {
        return { success: false, error: 'Your name is required.' };
    }

    if (!input.proposerEmail?.trim()) {
        return { success: false, error: 'Your email is required.' };
    }

    try {
        const response = await fetch(`${getApiUrl()}/v1/content/course-proposals`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                courseTitle: input.courseTitle.trim(),
                pitch: input.pitch?.trim() || undefined,
                audience: input.audience?.trim() || undefined,
                proposerName: input.proposerName.trim(),
                proposerEmail: input.proposerEmail.trim(),
                metadata: input.metadata?.trim() || undefined,
            }),
            cache: 'no-store',
        });

        if (!response.ok) {
            let payload: unknown = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }
            return {
                success: false,
                error: getFirstProblemDetailError(payload) || 'Failed to submit your proposal. Please try again.',
            };
        }

        return { success: true, message: 'Your course proposal has been submitted for review.' };
    } catch (error) {
        return {
            success: false,
            error: error instanceof Error ? error.message : 'Failed to submit your proposal. Please try again.',
        };
    }
}
