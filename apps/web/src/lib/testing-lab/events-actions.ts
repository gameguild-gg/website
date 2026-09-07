"use server";

import { getRequestAuthContext } from "@/auth";
import { isSupportedTimeZone, wallClockToUtcIso } from "@/lib/date-time-zone";
import {
  createServerClient,
  GeneratedApi,
  type ApiError,
  type Result,
  type TestingLabTestingApplicationVoteDecision,
  type TestingLabTestingEventApprovalMode,
  type TestingLabTestingEventMode,
  type SystemDayOfWeek,
  type TestingLabCreateTestingEventInput,
  type TestingLabTestingEventRecurrenceFrequency,
  type TestingLabTestingLearningCompletionRequirement,
  type TestingLabQuestionnaireOutput,
  type TestingLabQuestionnaireSchema,
  type TestingLabSaveTestingProjectApplicationDraftInput,
  type TestingLabTestingProjectApplicationProjection,
  type TestingLabTestingProjectBrief,
  type TestingLabTestingEventTemplateProjection,
} from "@game-guild/client";
import { revalidatePath } from "next/cache";

const EVENTS_PATH = "/workspace/testing-lab/events";

type ActionData<T> = [T] extends [void] ? null : T | null;
export type TestingEventActionResult<T = null> =
  | { success: true; data: ActionData<T>; message: string }
  | { success: false; error: string };

function createClient(requestAuth = getRequestAuthContext()) {
  return createServerClient({
    baseUrl:
      process.env.API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:8080",
    auth: { getAccessToken: async () => (await requestAuth).token },
    tenant: { getTenantId: async () => (await requestAuth).tenantId },
  });
}

function createModules(requestAuth = getRequestAuthContext()) {
  const client = createClient(requestAuth);
  return {
    events: new GeneratedApi.TestingLabTestingEventsModule(client),
    participation: new GeneratedApi.TestingLabTestingEventParticipationModule(
      client,
    ),
  };
}

function createTemplateModule(requestAuth = getRequestAuthContext()) {
  const client = createClient(requestAuth);
  return new GeneratedApi.TestingLabTestingEventTemplatesModule(client);
}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function optionalText(formData: FormData, key: string) {
  return text(formData, key) || null;
}

function checked(formData: FormData, key: string) {
  return formData.get(key) === "on" || formData.get(key) === "true";
}

export async function configureTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const projectApplicationSchema = jsonValue<TestingLabQuestionnaireSchema>(
    formData,
    "projectApplicationSchemaJson",
  );
  const testerRegistrationSchema = jsonValue<TestingLabQuestionnaireSchema>(
    formData,
    "testerRegistrationSchemaJson",
  );
  if (
    !eventId ||
    !text(formData, "generalRules") ||
    !text(formData, "candidateInstructions") ||
    !text(formData, "testerInstructions") ||
    !projectApplicationSchema ||
    !testerRegistrationSchema
  )
    return {
      success: false,
      error:
        "Rules, both instruction sets, and valid application and registration forms are required.",
    };
  return complete(
    createModules().events.putTestingEventsConfiguration(eventId, {
      generalRules: text(formData, "generalRules"),
      candidateInstructions: text(formData, "candidateInstructions"),
      testerInstructions: text(formData, "testerInstructions"),
      projectApplicationSchema,
      testerRegistrationSchema,
    }),
    "Event configuration saved.",
    eventId,
  );
}

export async function saveTestingEventTemplate(
  formData: FormData,
): Promise<TestingEventActionResult<TestingLabTestingEventTemplateProjection>> {
  const templateId = optionalText(formData, "templateId");
  const projectApplicationSchema = jsonValue<TestingLabQuestionnaireSchema>(
    formData,
    "projectApplicationSchemaJson",
  );
  const testerRegistrationSchema = jsonValue<TestingLabQuestionnaireSchema>(
    formData,
    "testerRegistrationSchemaJson",
  );
  if (
    !text(formData, "name") ||
    !text(formData, "generalRules") ||
    !text(formData, "candidateInstructions") ||
    !text(formData, "testerInstructions") ||
    !projectApplicationSchema ||
    !testerRegistrationSchema
  )
    return {
      success: false,
      error: "Name, rules, instructions, and both forms are required.",
    };
  const body = {
    name: text(formData, "name"),
    description: optionalText(formData, "description"),
    generalRules: text(formData, "generalRules"),
    candidateInstructions: text(formData, "candidateInstructions"),
    testerInstructions: text(formData, "testerInstructions"),
    projectApplicationSchema,
    testerRegistrationSchema,
    defaultMode: (text(formData, "defaultMode") ||
      "Online") as TestingLabTestingEventMode,
    defaultApprovalMode: (text(formData, "defaultApprovalMode") ||
      "ManagerOnly") as TestingLabTestingEventApprovalMode,
    defaultRequiresFeedback: checked(formData, "defaultRequiresFeedback"),
  };
  return complete(
    templateId
      ? createTemplateModule().putVTestingTemplates(templateId, "1", body)
      : createTemplateModule().postVTestingTemplates("1", body),
    templateId ? "New template revision saved." : "Event template created.",
    undefined,
  );
}

export async function setTestingEventTemplateArchived(
  formData: FormData,
): Promise<TestingEventActionResult<TestingLabTestingEventTemplateProjection>> {
  const templateId = text(formData, "templateId");
  if (!templateId) return { success: false, error: "Template is required." };
  return complete(
    checked(formData, "restore")
      ? createTemplateModule().postVTestingTemplatesRestore(templateId, "1")
      : createTemplateModule().postVTestingTemplatesArchive(templateId, "1"),
    checked(formData, "restore") ? "Template restored." : "Template archived.",
  );
}

function jsonValue<T>(formData: FormData, key: string): T | null {
  const raw = text(formData, key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

function optionalNumber(formData: FormData, key: string) {
  const raw = text(formData, key);
  if (!raw) return null;
  const value = Number(raw);
  return Number.isFinite(value) ? value : null;
}

function isoDate(formData: FormData, key: string, timeZoneId = "UTC") {
  const raw = text(formData, key);
  if (!raw) return null;
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(raw))
    return wallClockToUtcIso(raw, timeZoneId);
  const value = new Date(raw);
  return Number.isNaN(value.valueOf()) ? null : value.toISOString();
}

function revalidateEvent(eventId?: string) {
  revalidatePath("/workspace/testing-lab");
  revalidatePath(EVENTS_PATH);
  revalidatePath("/workspace/testing-lab/settings/templates");
  revalidatePath("/testing-lab");
  if (eventId) {
    revalidatePath(`${EVENTS_PATH}/${eventId}`);
    revalidatePath(`/testing-lab/events/${eventId}`);
  }
}

function errorDetail(error: unknown) {
  if (!error || typeof error !== "object") return null;
  const detail = Reflect.get(error, "detail");
  return typeof detail === "string" && detail.trim() ? detail : null;
}

function readableError(
  message: string | null | undefined,
  detail?: string | null,
) {
  if (detail?.trim()) return detail;
  if (message === "TestingLab.Validation") {
    return "Check that applications close before the event starts and the event ends after it starts.";
  }
  return message || "The Testing Lab event operation failed.";
}

async function complete<T>(
  operation: Promise<Result<T, ApiError>>,
  message: string,
  eventId?: string,
): Promise<TestingEventActionResult<T>> {
  try {
    const result = await operation;
    if (!result.ok)
      return {
        success: false,
        error: readableError(result.error.message, result.error.detail),
      };
    revalidateEvent(eventId);
    return {
      success: true,
      data: (result.data ?? null) as ActionData<T>,
      message,
    };
  } catch (error) {
    return {
      success: false,
      error: readableError(
        error instanceof Error ? error.message : null,
        errorDetail(error),
      ),
    };
  }
}

function required(
  formData: FormData,
  keys: string[],
  message: string,
): TestingEventActionResult | null {
  return keys.every((key) => text(formData, key))
    ? null
    : { success: false, error: message };
}

function recurrenceFrequency(
  value: string,
): TestingLabTestingEventRecurrenceFrequency | null {
  return value === "Daily" || value === "Weekly" || value === "Monthly"
    ? value
    : null;
}

function dayOfWeek(value: string): SystemDayOfWeek | null {
  const daysOfWeek: SystemDayOfWeek[] = [
    "Sunday",
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
  ];
  return daysOfWeek.includes(value as SystemDayOfWeek)
    ? (value as SystemDayOfWeek)
    : null;
}

function recurrenceInput(
  formData: FormData,
  timeZoneId: string,
): {
  data: TestingLabCreateTestingEventInput["recurrence"] | null;
  error: string | null;
} {
  const frequency = recurrenceFrequency(text(formData, "recurrenceFrequency"));
  if (!frequency) return { data: null, error: null };

  const interval = optionalNumber(formData, "recurrenceInterval") ?? 1;
  if (!Number.isInteger(interval) || interval < 1 || interval > 52)
    return { data: null, error: "Repeat interval must be between 1 and 52." };

  const daysOfWeek = formData
    .getAll("recurrenceDaysOfWeek")
    .map((value) => (typeof value === "string" ? dayOfWeek(value) : null))
    .filter((value): value is SystemDayOfWeek => value !== null);
  if (frequency === "Weekly" && daysOfWeek.length === 0)
    return {
      data: null,
      error: "Choose at least one weekday for a weekly event.",
    };

  const endsByDate = text(formData, "recurrenceEndMode") === "date";
  const endsAt = endsByDate
    ? isoDate(formData, "recurrenceEndsAt", timeZoneId)
    : null;
  const occurrenceCount = endsByDate
    ? null
    : optionalNumber(formData, "recurrenceOccurrenceCount");
  if (endsByDate && !endsAt)
    return { data: null, error: "Choose when the recurrence ends." };
  if (
    !endsByDate &&
    (!occurrenceCount ||
      !Number.isInteger(occurrenceCount) ||
      occurrenceCount < 1 ||
      occurrenceCount > 104)
  )
    return { data: null, error: "Choose between 1 and 104 occurrences." };

  return {
    data: {
      frequency,
      interval,
      daysOfWeek: daysOfWeek.length > 0 ? daysOfWeek : null,
      endsAt,
      occurrenceCount,
    },
    error: null,
  };
}

function eventInput(formData: FormData): {
  data: TestingLabCreateTestingEventInput | null;
  error: string | null;
} {
  const timeZoneId = text(formData, "timeZoneId") || "UTC";
  if (!isSupportedTimeZone(timeZoneId))
    return { data: null, error: "Choose a valid time zone." };
  const applicationsOpenAt = isoDate(
    formData,
    "applicationsOpenAt",
    timeZoneId,
  );
  const applicationsCloseAt = isoDate(
    formData,
    "applicationsCloseAt",
    timeZoneId,
  );
  const startsAt = isoDate(formData, "startsAt", timeZoneId);
  const endsAt = isoDate(formData, "endsAt", timeZoneId);
  if (!applicationsOpenAt || !applicationsCloseAt || !startsAt || !endsAt)
    return { data: null, error: "Enter valid event dates." };
  if (new Date(applicationsCloseAt) <= new Date(applicationsOpenAt))
    return { data: null, error: "Applications must close after they open." };
  if (new Date(startsAt) < new Date(applicationsCloseAt))
    return {
      data: null,
      error: "The event must start after applications close.",
    };
  if (new Date(endsAt) <= new Date(startsAt))
    return { data: null, error: "Event end must be after its start." };

  const recurrence = recurrenceInput(formData, timeZoneId);
  if (recurrence.error) return { data: null, error: recurrence.error };
  if (
    recurrence.data?.endsAt &&
    new Date(recurrence.data.endsAt) < new Date(startsAt)
  )
    return {
      data: null,
      error: "Recurrence end must not precede the event start.",
    };

  const templateRevisionId = optionalText(formData, "templateRevisionId");
  const hasInlineConfiguration = [
    "generalRules",
    "candidateInstructions",
    "testerInstructions",
  ].some((key) => text(formData, key));
  const configuration =
    !templateRevisionId && hasInlineConfiguration
      ? {
          generalRules: text(formData, "generalRules"),
          candidateInstructions: text(formData, "candidateInstructions"),
          testerInstructions: text(formData, "testerInstructions"),
          projectApplicationSchema: {
            title: "Project application",
            questions: [],
          },
          testerRegistrationSchema: {
            title: "Tester registration",
            questions: [],
          },
        }
      : undefined;
  if (
    configuration &&
    (!configuration.generalRules ||
      !configuration.candidateInstructions ||
      !configuration.testerInstructions)
  )
    return {
      data: null,
      error: "Choose a template or enter rules and instructions for the event.",
    };

  return {
    data: {
      name: text(formData, "name"),
      description: optionalText(formData, "description"),
      mode: (text(formData, "mode") || "Online") as TestingLabTestingEventMode,
      approvalMode: (text(formData, "approvalMode") ||
        "ManagerOnly") as TestingLabTestingEventApprovalMode,
      applicationsOpenAt,
      applicationsCloseAt,
      startsAt,
      endsAt,
      timeZoneId,
      requiresFeedback: checked(formData, "requiresFeedback"),
      templateRevisionId,
      configuration,
      recurrence: recurrence.data ?? undefined,
    },
    error: null,
  };
}

export async function createTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const invalid = required(
    formData,
    ["name", "applicationsOpenAt", "applicationsCloseAt", "startsAt", "endsAt"],
    "Name, application window, and event schedule are required.",
  );
  if (invalid) return invalid;
  const input = eventInput(formData);
  if (!input.data)
    return { success: false, error: input.error ?? "Enter valid event dates." };
  return complete(
    createModules().events.postTestingEvents(input.data),
    "Testing event created.",
  );
}

export async function updateTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const input = eventInput(formData);
  if (!eventId || !input.data)
    return {
      success: false,
      error: input.error ?? "Event and valid event details are required.",
    };
  const updateInput = { ...input.data };
  delete updateInput.recurrence;
  return complete(
    createModules().events.putTestingEvents(eventId, updateInput),
    "Testing event updated.",
    eventId,
  );
}

export async function deleteTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<boolean>> {
  const eventId = text(formData, "eventId");
  if (!eventId) return { success: false, error: "Event is required." };
  return complete(
    createModules().events.deleteTestingEvents(eventId),
    "Draft event deleted.",
    eventId,
  );
}

export async function archiveTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<boolean>> {
  const eventId = text(formData, "eventId");
  if (!eventId) return { success: false, error: "Event is required." };
  return complete(
    createModules().events.postTestingEventsArchive(eventId),
    "Testing event archived.",
    eventId,
  );
}

export async function restoreTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<boolean>> {
  const eventId = text(formData, "eventId");
  if (!eventId) return { success: false, error: "Event is required." };
  return complete(
    createModules().events.postTestingEventsRestore(eventId),
    "Testing event restored.",
    eventId,
  );
}

type EventTransition =
  | "open-applications"
  | "close-applications"
  | "schedule"
  | "activate"
  | "complete"
  | "cancel";

export async function transitionTestingEvent(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const transition = text(formData, "transition") as EventTransition;
  if (!eventId || !transition)
    return { success: false, error: "Event and transition are required." };
  const api = createModules().events;
  const operations: Record<
    EventTransition,
    () => Promise<Result<{ id?: string }, ApiError>>
  > = {
    "open-applications": () => api.postTestingEventsOpenApplications(eventId),
    "close-applications": () => api.postTestingEventsCloseApplications(eventId),
    schedule: () => api.postTestingEventsSchedule(eventId),
    activate: () => api.postTestingEventsActivate(eventId),
    complete: () => api.postTestingEventsComplete(eventId),
    cancel: () =>
      api.postTestingEventsCancel(eventId, {
        reason: text(formData, "reason"),
      }),
  };
  const operation = operations[transition];
  if (!operation)
    return { success: false, error: "Choose a valid event transition." };
  if (transition === "cancel" && !text(formData, "reason"))
    return { success: false, error: "A cancellation reason is required." };
  return complete(operation(), "Event status updated.", eventId);
}

function slotInput(formData: FormData) {
  const mode = (text(formData, "mode") ||
    "Online") as TestingLabTestingEventMode;
  const startsAt = isoDate(formData, "startsAt");
  const endsAt = isoDate(formData, "endsAt");
  if (!startsAt || !endsAt)
    return { error: "Enter a valid slot schedule." } as const;
  if (
    mode === "InPerson" &&
    (!text(formData, "campusName") || !text(formData, "roomName"))
  )
    return {
      error: "Campus and room are required for in-person slots.",
    } as const;
  if (mode === "Online" && !text(formData, "meetingUrl"))
    return { error: "A meeting URL is required for online slots." } as const;
  return {
    data: {
      mode,
      startsAt,
      endsAt,
      maxTesters: optionalNumber(formData, "maxTesters"),
      maxProjects: optionalNumber(formData, "maxProjects"),
      campusName: optionalText(formData, "campusName"),
      roomName: optionalText(formData, "roomName"),
      meetingUrl: optionalText(formData, "meetingUrl"),
      locationId: optionalText(formData, "locationId"),
    },
  } as const;
}

export async function createTestingEventSlot(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  if (!eventId) return { success: false, error: "Event is required." };
  const input = slotInput(formData);
  if ("error" in input && input.error)
    return { success: false, error: input.error };
  return complete(
    createModules().events.postTestingEventsSlots(eventId, input.data),
    "Event slot created.",
    eventId,
  );
}

export async function updateTestingEventSlot(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const slotId = text(formData, "slotId");
  if (!eventId || !slotId)
    return { success: false, error: "Event and slot are required." };
  const input = slotInput(formData);
  if ("error" in input && input.error)
    return { success: false, error: input.error };
  return complete(
    createModules().events.putTestingEventsSlots(eventId, slotId, input.data),
    "Event slot updated.",
    eventId,
  );
}

export async function deleteTestingEventSlot(
  formData: FormData,
): Promise<TestingEventActionResult<boolean>> {
  const eventId = text(formData, "eventId");
  const slotId = text(formData, "slotId");
  if (!eventId || !slotId)
    return { success: false, error: "Event and slot are required." };
  return complete(
    createModules().events.deleteTestingEventsSlots(eventId, slotId),
    "Event slot removed.",
    eventId,
  );
}

export async function addTestingEventCommitteeMember(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const userId = text(formData, "userId");
  if (!eventId || !userId)
    return { success: false, error: "Event and reviewer are required." };
  return complete(
    createModules().events.postTestingEventsCommittee(eventId, {
      userId,
      isChair: checked(formData, "isChair"),
    }),
    "Committee member added.",
    eventId,
  );
}

export async function removeTestingEventCommitteeMember(
  formData: FormData,
): Promise<TestingEventActionResult<boolean>> {
  const eventId = text(formData, "eventId");
  const userId = text(formData, "userId");
  if (!eventId || !userId)
    return { success: false, error: "Event and reviewer are required." };
  return complete(
    createModules().events.deleteTestingEventsCommittee(eventId, userId),
    "Committee member removed.",
    eventId,
  );
}

export async function configureTestingEventLearning(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const invalid = required(
    formData,
    ["eventId", "courseId", "learningActivityId", "requirement"],
    "Event, course, activity, and completion requirement are required.",
  );
  if (invalid) return invalid;
  const eventId = text(formData, "eventId");
  return complete(
    createModules().events.putTestingEventsLearning(eventId, {
      courseId: text(formData, "courseId"),
      cohortId: optionalText(formData, "cohortId"),
      learningActivityId: text(formData, "learningActivityId"),
      requirement: text(
        formData,
        "requirement",
      ) as TestingLabTestingLearningCompletionRequirement,
    }),
    "Learning evidence configured.",
    eventId,
  );
}

export async function submitTestingProjectApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const eventId = text(formData, "eventId");
  const projectId = text(formData, "projectId");
  const projectVersionId = text(formData, "projectVersionId");
  if (!eventId || !projectId || !projectVersionId)
    return {
      success: false,
      error: "Event, project, and an accessible project version are required.",
    };
  return complete(
    createModules().events.postTestingEventsApplications(eventId, {
      projectId,
      projectVersionId,
      preferredAvailability: optionalText(formData, "preferredAvailability"),
    }),
    "Project application submitted.",
    eventId,
  );
}

export async function saveTestingProjectApplicationDraft(
  formData: FormData,
): Promise<
  TestingEventActionResult<TestingLabTestingProjectApplicationProjection>
> {
  const eventId = text(formData, "eventId");
  const projectId = text(formData, "projectId");
  let applicationId = text(formData, "applicationId");
  if (!eventId || !projectId)
    return { success: false, error: "Event and project are required." };

  const modules = createModules();
  try {
    if (!applicationId) {
      const created = await modules.events.postTestingEventsApplicationsDrafts(
        eventId,
        { projectId },
      );
      if (!created.ok)
        return {
          success: false,
          error: readableError(created.error.message, created.error.detail),
        };
      applicationId = created.data.id ?? "";
    }
    if (!applicationId)
      return {
        success: false,
        error: "The application draft could not be created.",
      };

    const body: TestingLabSaveTestingProjectApplicationDraftInput = {
      projectVersionId: optionalText(formData, "projectVersionId") ?? undefined,
      brief:
        jsonValue<TestingLabTestingProjectBrief>(formData, "briefJson") ??
        undefined,
      feedbackQuestionnaire:
        jsonValue<TestingLabQuestionnaireSchema>(
          formData,
          "feedbackQuestionnaireJson",
        ) ?? undefined,
      eventApplicationResponse:
        jsonValue<TestingLabQuestionnaireOutput>(
          formData,
          "eventApplicationResponseJson",
        ) ?? undefined,
      acceptedRules: formData.has("acceptedRules")
        ? checked(formData, "acceptedRules")
        : undefined,
      preferredAvailability: optionalText(formData, "preferredAvailability"),
      submittedAssetReferenceIds:
        jsonValue<string[]>(formData, "submittedAssetReferenceIdsJson") ?? [],
    };
    const saved = await modules.events.putTestingEventsApplicationsDraft(
      applicationId,
      body,
    );
    if (!saved.ok)
      return {
        success: false,
        error: readableError(saved.error.message, saved.error.detail),
      };

    if (
      text(formData, "intent") === "submit" &&
      saved.data.status === "Draft"
    ) {
      const submitted =
        await modules.events.postTestingEventsApplicationsSubmit(applicationId);
      if (!submitted.ok)
        return {
          success: false,
          error: readableError(submitted.error.message, submitted.error.detail),
        };
      return {
        success: true,
        data: submitted.data,
        message: "Project application submitted.",
      };
    }
    return {
      success: true,
      data: saved.data,
      message: "Application draft saved.",
    };
  } catch (error) {
    return {
      success: false,
      error: readableError(
        error instanceof Error ? error.message : null,
        errorDetail(error),
      ),
    };
  }
}

export async function updateTestingProjectApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  const projectVersionId = text(formData, "projectVersionId");
  if (!applicationId || !projectVersionId)
    return {
      success: false,
      error: "Application and an accessible project version are required.",
    };
  return complete(
    createModules().events.putTestingEventsApplications(applicationId, {
      projectVersionId,
      preferredAvailability: optionalText(formData, "preferredAvailability"),
    }),
    "Project application updated.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function withdrawTestingProjectApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  if (!applicationId)
    return { success: false, error: "Application is required." };
  return complete(
    createModules().events.postTestingEventsApplicationsWithdraw(applicationId),
    "Project application withdrawn.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function beginTestingEventApplicationReview(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  if (!applicationId)
    return { success: false, error: "Application is required." };
  return complete(
    createModules().events.postTestingEventsApplicationsReview(applicationId),
    "Application review started.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function voteOnTestingEventApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  const decision = text(
    formData,
    "decision",
  ) as TestingLabTestingApplicationVoteDecision;
  if (!applicationId || !decision)
    return { success: false, error: "Application and vote are required." };
  return complete(
    createModules().events.postTestingEventsApplicationsVotes(applicationId, {
      decision,
      comments: optionalText(formData, "comments"),
    }),
    "Committee vote recorded.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function approveTestingEventApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  const slotId = text(formData, "slotId");
  if (!applicationId || !slotId)
    return {
      success: false,
      error: "Application and slot are required for approval.",
    };
  return complete(
    createModules().events.postTestingEventsApplicationsApprove(applicationId, {
      slotId,
      rationale: optionalText(formData, "rationale"),
    }),
    "Project application approved.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function rejectTestingEventApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  const rationale = text(formData, "rationale");
  if (!applicationId)
    return { success: false, error: "Application is required." };
  if (!rationale)
    return { success: false, error: "A rejection rationale is required." };
  return complete(
    createModules().events.postTestingEventsApplicationsReject(applicationId, {
      slotId: null,
      rationale,
    }),
    "Project application rejected.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function waitlistTestingEventApplication(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  if (!applicationId)
    return { success: false, error: "Application is required." };
  return complete(
    createModules().events.postTestingEventsApplicationsWaitlist(
      applicationId,
      {
        slotId: null,
        rationale: optionalText(formData, "rationale"),
      },
    ),
    "Project application waitlisted.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function assignTestingEventApplicationSlot(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const applicationId = text(formData, "applicationId");
  const slotId = text(formData, "slotId");
  if (!applicationId || !slotId)
    return { success: false, error: "Application and slot are required." };
  return complete(
    createModules().events.putTestingEventsApplicationsSlot(applicationId, {
      slotId,
    }),
    "Application slot updated.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function registerForTestingEventSlot(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const slotId = text(formData, "slotId");
  if (!slotId) return { success: false, error: "Slot is required." };
  return complete(
    createModules().participation.postTestingEventsSlotsRegistrations(slotId, {
      notes: optionalText(formData, "notes"),
      acceptedRules: checked(formData, "acceptedRules"),
      registrationResponse: jsonValue<TestingLabQuestionnaireOutput>(
        formData,
        "registrationResponseJson",
      ) ?? { answers: [] },
    }),
    "Testing slot registration submitted.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function updateTestingEventAttendance(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const registrationId = text(formData, "registrationId");
  const attendance = text(formData, "attendance");
  if (!registrationId || !attendance)
    return {
      success: false,
      error: "Registration and attendance action are required.",
    };
  const api = createModules().participation;
  const operations: Record<
    string,
    () => Promise<Result<{ id?: string }, ApiError>>
  > = {
    "check-in": () => api.postTestingEventsRegistrationsCheckIn(registrationId),
    "check-out": () =>
      api.postTestingEventsRegistrationsCheckOut(registrationId),
    "no-show": () => api.postTestingEventsRegistrationsNoShow(registrationId),
    complete: () => api.postTestingEventsRegistrationsComplete(registrationId),
  };
  const operation = operations[attendance];
  if (!operation)
    return { success: false, error: "Choose a valid attendance action." };
  return complete(
    operation(),
    "Attendance updated.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function assignTestedProjectToRegistration(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const registrationId = text(formData, "registrationId");
  const applicationId = text(formData, "applicationId");
  if (!registrationId || !applicationId)
    return {
      success: false,
      error: "Registration and approved project are required.",
    };
  return complete(
    createModules().participation.postTestingEventsRegistrationsTestedProjects(
      registrationId,
      { applicationId },
    ),
    "Tested project assigned.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function cancelTestingEventRegistration(
  formData: FormData,
): Promise<TestingEventActionResult<unknown>> {
  const registrationId = text(formData, "registrationId");
  if (!registrationId)
    return { success: false, error: "Registration is required." };
  return complete(
    createModules().participation.deleteTestingEventsRegistrations(
      registrationId,
    ),
    "Testing registration cancelled.",
    optionalText(formData, "eventId") ?? undefined,
  );
}

export async function submitTestingEventFeedback(
  formData: FormData,
): Promise<TestingEventActionResult<{ id?: string }>> {
  const obligationId = text(formData, "obligationId");
  const responses = jsonValue<TestingLabQuestionnaireOutput>(
    formData,
    "responsesJson",
  );
  const questionnaireRevisionId = optionalText(
    formData,
    "questionnaireRevisionId",
  );
  const overallRating = optionalNumber(formData, "overallRating");
  if (!obligationId)
    return { success: false, error: "Feedback obligation is required." };
  if (
    !responses ||
    !questionnaireRevisionId ||
    overallRating === null ||
    overallRating < 1 ||
    overallRating > 10
  ) {
    return {
      success: false,
      error:
        "Complete the assigned questionnaire and provide a rating from 1 to 10.",
    };
  }

  return complete(
    createModules().participation.postTestingEventsFeedbackObligationsFeedback(
      obligationId,
      {
        questionnaireRevisionId,
        responses,
        overallRating,
        wouldRecommend: checked(formData, "wouldRecommend"),
        additionalNotes: optionalText(formData, "additionalNotes"),
      },
    ),
    "Required project feedback submitted.",
    optionalText(formData, "eventId") ?? undefined,
  );
}
