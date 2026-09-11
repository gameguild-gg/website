"use server";

import { getRequestAuthContext } from "@/auth";
import type {
  LearningCoursesLessonContentFormat,
  LearningCoursesProgramContentType,
  LearningCoursesVisibility,
} from "@game-guild/client";

const apiBaseUrl = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:8080"
).replace(/\/$/, "");

export type AiProposalKind =
  | "ReplaceDocument"
  | "InsertAtCursor"
  | "LexicalPatch"
  | "QuizPatch"
  | "MetadataPatch";

export interface AuthoringContentPayload {
  title: string;
  slug: string;
  description?: string | null;
  type: LearningCoursesProgramContentType;
  body?: string | null;
  jsonBody?: Record<string, unknown> | null;
  lessonFormat?: LearningCoursesLessonContentFormat | null;
  activitySettings?: Record<string, unknown> | null;
  isRequired: boolean;
  estimatedMinutes?: number | null;
  estimatedMinutesSource: "Auto" | "Manual";
  visibility: LearningCoursesVisibility;
}

export interface AuthoringDraft {
  id: string;
  programId: string;
  contentId: string;
  payload: AuthoringContentPayload;
  basePublishedVersion: number;
  revision: number;
  eTag: string;
  lastEditedBy: string;
  lastEditedAt: string;
}

export interface AiCreditUsage {
  availableSoftCredits: number;
  maximumEstimatedCost: number;
  inputTokens: number;
  outputTokens: number;
  settledCost: number;
  releasedAmount: number;
  currency: string;
}

export interface AiProposal {
  id: string;
  runId: string;
  baseDraftRevision: number;
  kind: AiProposalKind;
  status: "Pending" | "Applied" | "Discarded";
  originalContent: string;
  proposedContent: string;
  proposedAt: string;
}

export interface AiAuthoringRun {
  id: string;
  conversationId: string;
  contentId: string;
  baseDraftRevision: number;
  proposalKind: AiProposalKind;
  status: "Queued" | "Reserved" | "Running" | "Completed" | "Failed" | "Cancelled";
  instruction: string;
  provider?: string | null;
  model?: string | null;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  usage: AiCreditUsage;
  proposal?: AiProposal | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface AiEntitlement {
  availableSoftCredits: number;
  reservedSoftCredits: number;
  settledSoftCredits: number;
  currency: string;
}

export interface AiAuthoringMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  runId?: string | null;
  createdAt: string;
}

export interface AiAuthoringConversation {
  id: string;
  contentId: string;
  authorId: string;
  lastMessageAt: string;
  messages: AiAuthoringMessage[];
}

export type AuthoringActionResult<T> =
  | { success: true; data: T }
  | {
      success: false;
      error: string;
      status: number;
      code?: string;
      currentRevision?: number;
    };

async function authoringRequest<T>(
  path: string,
  init?: RequestInit,
): Promise<AuthoringActionResult<T>> {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) {
    return {
      success: false,
      error: "You must be signed in to edit this lesson.",
      status: 401,
    };
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
    cache: "no-store",
  });
  const body = (await response.json().catch(() => null)) as
    | Record<string, unknown>
    | T
    | null;
  if (!response.ok) {
    const details = (body ?? {}) as Record<string, unknown>;
    return {
      success: false,
      error:
        (typeof details.detail === "string" && details.detail) ||
        (typeof details.title === "string" && details.title) ||
        response.statusText ||
        "Authoring request failed.",
      status: response.status,
      code: typeof details.code === "string" ? details.code : undefined,
      currentRevision:
        typeof details.currentRevision === "number"
          ? details.currentRevision
          : undefined,
    };
  }
  return { success: true, data: body as T };
}

function path(courseId: string, contentId: string, suffix = "") {
  return `/v1/courses/${encodeURIComponent(courseId)}/content/${encodeURIComponent(contentId)}/authoring${suffix}`;
}

export async function getAuthoringDraft(courseId: string, contentId: string) {
  return authoringRequest<AuthoringDraft>(path(courseId, contentId));
}

export async function saveAuthoringDraft(
  courseId: string,
  contentId: string,
  revision: number,
  payload: AuthoringContentPayload,
) {
  return authoringRequest<AuthoringDraft>(path(courseId, contentId), {
    method: "PUT",
    body: JSON.stringify({ revision, payload }),
  });
}

export async function publishAuthoringDraft(
  courseId: string,
  contentId: string,
  revision: number,
) {
  return authoringRequest<{ draft: AuthoringDraft }>(
    path(courseId, contentId, "/publish"),
    { method: "POST", body: JSON.stringify({ revision }) },
  );
}

export async function getAiEntitlement(courseId: string, contentId: string) {
  return authoringRequest<AiEntitlement>(
    path(courseId, contentId, "/ai/entitlement"),
  );
}

export async function getAiConversations(courseId: string, contentId: string) {
  return authoringRequest<AiAuthoringConversation[]>(
    path(courseId, contentId, "/ai/conversations"),
  );
}

export async function createAiAuthoringRun(
  courseId: string,
  contentId: string,
  request: {
    conversationId?: string | null;
    draftRevision: number;
    instruction: string;
    proposalKind: AiProposalKind;
    selection?: string | null;
    idempotencyKey: string;
  },
) {
  return authoringRequest<AiAuthoringRun>(path(courseId, contentId, "/ai/runs"), {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function getAiAuthoringRun(
  courseId: string,
  contentId: string,
  runId: string,
) {
  return authoringRequest<AiAuthoringRun>(
    path(courseId, contentId, `/ai/runs/${encodeURIComponent(runId)}`),
  );
}

export async function applyAiProposal(
  courseId: string,
  contentId: string,
  proposalId: string,
  draftRevision: number,
  cursorOffset?: number,
) {
  return authoringRequest<AuthoringDraft>(
    path(courseId, contentId, `/ai/proposals/${encodeURIComponent(proposalId)}/apply`),
    {
      method: "POST",
      body: JSON.stringify({ draftRevision, cursorOffset }),
    },
  );
}

export async function discardAiProposal(
  courseId: string,
  contentId: string,
  proposalId: string,
) {
  return authoringRequest<AiProposal>(
    path(courseId, contentId, `/ai/proposals/${encodeURIComponent(proposalId)}`),
    { method: "DELETE" },
  );
}
