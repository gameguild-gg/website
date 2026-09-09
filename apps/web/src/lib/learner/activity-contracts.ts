import type {
    LearningAssessmentsAssessmentType,
    LearningAssessmentsSubmissionModality,
    LearningAssessmentsSubmitAssessmentInput,
} from '@game-guild/client';

export type LearnerContentActivityKind = 'discussion' | 'reflection' | 'survey';

export function getPreferredSubmissionModality(
    assessmentType: LearningAssessmentsAssessmentType | undefined,
    configured: LearningAssessmentsSubmissionModality | undefined,
): LearningAssessmentsSubmissionModality {
    if (assessmentType === 'Quiz') return 'None';
    if (assessmentType === 'Project') return 'Project';
    return configured && configured !== 'None' ? configured : 'Text';
}

export function buildAssessmentPayload(
    modality: LearningAssessmentsSubmissionModality,
    value: string,
): LearningAssessmentsSubmitAssessmentInput {
    const normalized = value.trim();
    if (!normalized) throw new Error('A submission response is required.');

    switch (modality) {
        case 'File':
            return { filePayload: normalized };
        case 'Url':
            return { urlPayload: normalized };
        case 'Code':
            return { codePayload: normalized };
        case 'Media':
            return { mediaPayload: normalized };
        case 'Project':
            return { projectPayload: normalized };
        case 'Text':
            return { textPayload: normalized };
        case 'None':
        default:
            throw new Error('This assessment does not have a valid submission method.');
    }
}

export function buildContentActivityPayload(kind: LearnerContentActivityKind, value: string) {
    const normalized = value.trim();
    if (!normalized) throw new Error('A response is required.');

    switch (kind) {
        case 'discussion':
            return { kind: 'discussion' as const, body: normalized };
        case 'reflection':
            return { kind: 'reflection' as const, body: normalized };
        case 'survey':
            return { kind: 'survey' as const, answers: { response: normalized } };
    }
}
