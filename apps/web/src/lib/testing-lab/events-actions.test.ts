import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getRequestAuthContext: vi.fn(),
  revalidatePath: vi.fn(),
  events: {
    postTestingEventsArchive: vi.fn(),
    postTestingEvents: vi.fn(),
    postTestingEventsSlots: vi.fn(),
    postTestingEventsApplicationsReject: vi.fn(),
    postTestingEventsOpenApplications: vi.fn(),
    postTestingEventsCommittee: vi.fn(),
    postTestingEventsRestore: vi.fn(),
    putTestingEventsConfiguration: vi.fn(),
  },
  participation: {
    deleteTestingEventsRegistrations: vi.fn(),
    postTestingEventsFeedbackObligationsFeedback: vi.fn(),
  },
  templates: {
    postVTestingTemplates: vi.fn(),
    putVTestingTemplates: vi.fn(),
    postVTestingTemplatesArchive: vi.fn(),
    postVTestingTemplatesRestore: vi.fn(),
  },
}));

vi.mock("@/auth", () => ({
  getRequestAuthContext: mocks.getRequestAuthContext,
}));

vi.mock("next/cache", () => ({
  revalidatePath: mocks.revalidatePath,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: vi.fn(() => ({})),
  GeneratedApi: {
    TestingLabTestingEventsModule: vi.fn(
      function TestingLabTestingEventsModule() {
        return mocks.events;
      },
    ),
    TestingLabTestingEventParticipationModule: vi.fn(
      function TestingLabTestingEventParticipationModule() {
        return mocks.participation;
      },
    ),
    TestingLabTestingEventTemplatesModule: vi.fn(
      function TestingLabTestingEventTemplatesModule() {
        return mocks.templates;
      },
    ),
  },
}));

import {
  addTestingEventCommitteeMember,
  archiveTestingEvent,
  cancelTestingEventRegistration,
  configureTestingEvent,
  createTestingEvent,
  createTestingEventSlot,
  rejectTestingEventApplication,
  restoreTestingEvent,
  saveTestingEventTemplate,
  setTestingEventTemplateArchived,
  submitTestingEventFeedback,
  transitionTestingEvent,
} from "./events-actions";

function form(values: Record<string, string>) {
  const data = new FormData();
  data.set("generalRules", "Respect the code of conduct.");
  data.set("candidateInstructions", "Provide a playable build.");
  data.set("testerInstructions", "Complete the assigned tasks.");
  Object.entries(values).forEach(([key, value]) => data.set(key, value));
  return data;
}

describe("Testing Lab event actions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getRequestAuthContext.mockResolvedValue({
      token: "access-token",
      tenantId: "tenant-1",
      session: { tenantId: "tenant-1" },
    });
  });

  it("creates a draft event without requiring its participation configuration", async () => {
    mocks.events.postTestingEvents.mockResolvedValue({
      ok: true,
      data: { id: "event-1", name: "Campus showcase" },
    });

    const data = form({
      name: "Campus showcase",
      timeZoneId: "America/Sao_Paulo",
      mode: "InPerson",
      approvalMode: "Committee",
      applicationsOpenAt: "2026-08-01T09:00",
      applicationsCloseAt: "2026-08-05T18:00",
      startsAt: "2026-08-08T18:00",
      endsAt: "2026-08-08T21:00",
      requiresFeedback: "true",
    });
    data.delete("generalRules");
    data.delete("candidateInstructions");
    data.delete("testerInstructions");

    const result = await createTestingEvent(data);

    expect(result.success).toBe(true);
    expect(mocks.events.postTestingEvents).toHaveBeenCalledWith(
      expect.objectContaining({
        name: "Campus showcase",
        mode: "InPerson",
        approvalMode: "Committee",
        timeZoneId: "America/Sao_Paulo",
        requiresFeedback: true,
        applicationsOpenAt: "2026-08-01T12:00:00.000Z",
        applicationsCloseAt: "2026-08-05T21:00:00.000Z",
        startsAt: "2026-08-08T21:00:00.000Z",
        endsAt: "2026-08-09T00:00:00.000Z",
        configuration: undefined,
      }),
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/workspace/testing-lab/events",
    );
  });

  it("archives and restores an event through the generated client", async () => {
    mocks.events.postTestingEventsArchive.mockResolvedValue({
      ok: true,
      data: true,
    });
    mocks.events.postTestingEventsRestore.mockResolvedValue({
      ok: true,
      data: true,
    });

    const archived = await archiveTestingEvent(form({ eventId: "event-1" }));
    const restored = await restoreTestingEvent(form({ eventId: "event-1" }));

    expect(archived).toMatchObject({
      success: true,
      data: true,
      message: "Testing event archived.",
    });
    expect(restored).toMatchObject({
      success: true,
      data: true,
      message: "Testing event restored.",
    });
    expect(mocks.events.postTestingEventsArchive).toHaveBeenCalledWith(
      "event-1",
    );
    expect(mocks.events.postTestingEventsRestore).toHaveBeenCalledWith(
      "event-1",
    );
  });

  it("shows the API validation detail instead of its generic validation code", async () => {
    mocks.events.postTestingEvents.mockResolvedValue({
      ok: false,
      error: {
        message: "TestingLab.Validation",
        detail: "The event must start after applications close.",
      },
    });

    const result = await createTestingEvent(
      form({
        name: "Campus showcase",
        applicationsOpenAt: "2026-08-01T09:00",
        applicationsCloseAt: "2026-08-05T18:00",
        startsAt: "2026-08-08T18:00",
        endsAt: "2026-08-08T21:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "The event must start after applications close.",
    });
  });

  it("replaces a generic TestingLab.Validation response with actionable guidance", async () => {
    mocks.events.postTestingEvents.mockResolvedValue({
      ok: false,
      error: { message: "TestingLab.Validation" },
    });

    const result = await createTestingEvent(
      form({
        name: "Campus showcase",
        applicationsOpenAt: "2026-08-01T09:00",
        applicationsCloseAt: "2026-08-05T18:00",
        startsAt: "2026-08-08T18:00",
        endsAt: "2026-08-08T21:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error:
        "Check that applications close before the event starts and the event ends after it starts.",
    });
  });

  it("shows validation detail when the generated client rejects the request", async () => {
    mocks.events.postTestingEvents.mockRejectedValue({
      message: "TestingLab.Validation",
      detail: "Recurrence end must not precede the event start.",
    });

    const result = await createTestingEvent(
      form({
        name: "Recurring playtest",
        applicationsOpenAt: "2026-08-01T09:00",
        applicationsCloseAt: "2026-08-05T18:00",
        startsAt: "2026-08-08T18:00",
        endsAt: "2026-08-08T21:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "Recurrence end must not precede the event start.",
    });
  });

  it("rejects an application window that closes before it opens", async () => {
    const result = await createTestingEvent(
      form({
        name: "Invalid application window",
        applicationsOpenAt: "2026-08-05T18:00",
        applicationsCloseAt: "2026-08-05T09:00",
        startsAt: "2026-08-08T18:00",
        endsAt: "2026-08-08T21:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "Applications must close after they open.",
    });
    expect(mocks.events.postTestingEvents).not.toHaveBeenCalled();
  });

  it("rejects an event that starts before applications close", async () => {
    const result = await createTestingEvent(
      form({
        name: "Invalid event schedule",
        applicationsOpenAt: "2026-08-01T09:00",
        applicationsCloseAt: "2026-08-08T18:00",
        startsAt: "2026-08-08T09:00",
        endsAt: "2026-08-08T21:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "The event must start after applications close.",
    });
    expect(mocks.events.postTestingEvents).not.toHaveBeenCalled();
  });

  it("forwards a weekly recurrence to the generated client", async () => {
    mocks.events.postTestingEvents.mockResolvedValue({
      ok: true,
      data: { id: "event-2" },
    });
    const input = form({
      name: "Weekly playtest",
      mode: "Online",
      approvalMode: "ManagerOnly",
      applicationsOpenAt: "2026-08-01T09:00",
      applicationsCloseAt: "2026-08-02T18:00",
      startsAt: "2026-08-03T18:00",
      endsAt: "2026-08-03T20:00",
      recurrenceFrequency: "Weekly",
      recurrenceInterval: "1",
      recurrenceEndMode: "count",
      recurrenceOccurrenceCount: "3",
    });
    input.append("recurrenceDaysOfWeek", "Monday");

    const result = await createTestingEvent(input);

    expect(result.success).toBe(true);
    expect(mocks.events.postTestingEvents).toHaveBeenCalledWith(
      expect.objectContaining({
        recurrence: {
          frequency: "Weekly",
          interval: 1,
          daysOfWeek: ["Monday"],
          occurrenceCount: 3,
          endsAt: null,
        },
      }),
    );
  });

  it("rejects a recurrence end before the first event starts", async () => {
    const result = await createTestingEvent(
      form({
        name: "Invalid recurring playtest",
        applicationsOpenAt: "2026-08-01T09:00",
        applicationsCloseAt: "2026-08-02T18:00",
        startsAt: "2026-08-03T18:00",
        endsAt: "2026-08-03T20:00",
        recurrenceFrequency: "Daily",
        recurrenceInterval: "1",
        recurrenceEndMode: "date",
        recurrenceEndsAt: "2026-08-03T17:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "Recurrence end must not precede the event start.",
    });
    expect(mocks.events.postTestingEvents).not.toHaveBeenCalled();
  });

  it("requires campus and room for an in-person slot before calling the API", async () => {
    const result = await createTestingEventSlot(
      form({
        eventId: "event-1",
        mode: "InPerson",
        startsAt: "2026-08-08T18:00",
        endsAt: "2026-08-08T20:00",
      }),
    );

    expect(result).toEqual({
      success: false,
      error: "Campus and room are required for in-person slots.",
    });
    expect(mocks.events.postTestingEventsSlots).not.toHaveBeenCalled();
  });

  it("requires a rationale to reject a project application", async () => {
    const result = await rejectTestingEventApplication(
      form({ applicationId: "application-1", rationale: " " }),
    );

    expect(result).toEqual({
      success: false,
      error: "A rejection rationale is required.",
    });
    expect(
      mocks.events.postTestingEventsApplicationsReject,
    ).not.toHaveBeenCalled();
  });

  it("maps an event lifecycle operation to its generated client method", async () => {
    mocks.events.postTestingEventsOpenApplications.mockResolvedValue({
      ok: true,
      data: { id: "event-1", status: "ApplicationsOpen" },
    });

    const result = await transitionTestingEvent(
      form({ eventId: "event-1", transition: "open-applications" }),
    );

    expect(result.success).toBe(true);
    expect(mocks.events.postTestingEventsOpenApplications).toHaveBeenCalledWith(
      "event-1",
    );
  });

  it("adds a committee member through the generated event client", async () => {
    mocks.events.postTestingEventsCommittee.mockResolvedValue({
      ok: true,
      data: { id: "member-1", userId: "user-1", isChair: true },
    });

    const result = await addTestingEventCommitteeMember(
      form({ eventId: "event-1", userId: "user-1", isChair: "on" }),
    );

    expect(result.success).toBe(true);
    expect(mocks.events.postTestingEventsCommittee).toHaveBeenCalledWith(
      "event-1",
      {
        userId: "user-1",
        isChair: true,
      },
    );
  });

  it("cancels the current tester registration through the participation client", async () => {
    mocks.participation.deleteTestingEventsRegistrations.mockResolvedValue({
      ok: true,
      data: true,
    });

    const result = await cancelTestingEventRegistration(
      form({ eventId: "event-1", registrationId: "registration-1" }),
    );

    expect(result.success).toBe(true);
    expect(
      mocks.participation.deleteTestingEventsRegistrations,
    ).toHaveBeenCalledWith("registration-1");
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/testing-lab/events/event-1",
    );
  });

  it("submits required structured feedback for an assigned project", async () => {
    mocks.participation.postTestingEventsFeedbackObligationsFeedback.mockResolvedValue(
      {
        ok: true,
        data: { id: "feedback-1" },
      },
    );

    const result = await submitTestingEventFeedback(
      form({
        eventId: "event-1",
        obligationId: "obligation-1",
        questionnaireRevisionId: "11111111-1111-1111-1111-111111111111",
        responsesJson: JSON.stringify({
          answers: [
            {
              questionId: "clarity",
              textValue: "The onboarding and controls are clear.",
            },
          ],
        }),
        overallRating: "8",
        wouldRecommend: "true",
        additionalNotes: "Retest after the tutorial polish.",
      }),
    );

    expect(result.success).toBe(true);
    expect(
      mocks.participation.postTestingEventsFeedbackObligationsFeedback,
    ).toHaveBeenCalledWith("obligation-1", {
      questionnaireRevisionId: "11111111-1111-1111-1111-111111111111",
      responses: {
        answers: [
          {
            questionId: "clarity",
            textValue: "The onboarding and controls are clear.",
          },
        ],
      },
      overallRating: 8,
      wouldRecommend: true,
      additionalNotes: "Retest after the tutorial polish.",
    });
  });

  it("saves a complete draft event configuration through the generated client", async () => {
    mocks.events.putTestingEventsConfiguration.mockResolvedValue({
      ok: true,
      data: { id: "event-1" },
    });
    const schema = JSON.stringify({ title: "Application", questions: [] });
    const result = await configureTestingEvent(
      form({
        eventId: "event-1",
        generalRules: "Respect the code of conduct.",
        candidateInstructions: "Provide a playable build.",
        testerInstructions: "Complete the assigned tasks.",
        projectApplicationSchemaJson: schema,
        testerRegistrationSchemaJson: schema,
      }),
    );

    expect(result.success).toBe(true);
    expect(mocks.events.putTestingEventsConfiguration).toHaveBeenCalledWith(
      "event-1",
      expect.objectContaining({
        generalRules: "Respect the code of conduct.",
        projectApplicationSchema: { title: "Application", questions: [] },
      }),
    );
  });

  it("creates and archives a versioned event template", async () => {
    mocks.templates.postVTestingTemplates.mockResolvedValue({
      ok: true,
      data: { id: "template-1", currentRevisionNumber: 1 },
    });
    mocks.templates.postVTestingTemplatesArchive.mockResolvedValue({
      ok: true,
      data: { id: "template-1", isArchived: true },
    });
    const schema = JSON.stringify({ title: "Application", questions: [] });
    const created = await saveTestingEventTemplate(
      form({
        name: "Campus playtest",
        generalRules: "Respect the code of conduct.",
        candidateInstructions: "Provide a playable build.",
        testerInstructions: "Complete the assigned tasks.",
        projectApplicationSchemaJson: schema,
        testerRegistrationSchemaJson: schema,
        defaultMode: "InPerson",
        defaultApprovalMode: "Committee",
        defaultRequiresFeedback: "on",
      }),
    );
    const archived = await setTestingEventTemplateArchived(
      form({ templateId: "template-1" }),
    );

    expect(created.success).toBe(true);
    expect(archived.success).toBe(true);
    expect(mocks.templates.postVTestingTemplates).toHaveBeenCalledWith(
      "1",
      expect.objectContaining({ defaultRequiresFeedback: true }),
    );
    expect(mocks.templates.postVTestingTemplatesArchive).toHaveBeenCalledWith(
      "template-1",
      "1",
    );
  });

  it("validates feedback before calling the participation client", async () => {
    const result = await submitTestingEventFeedback(
      form({
        obligationId: "obligation-1",
        responsesJson: '{"answers":[]}',
        overallRating: "11",
      }),
    );

    expect(result).toEqual({
      success: false,
      error:
        "Complete the assigned questionnaire and provide a rating from 1 to 10.",
    });
    expect(
      mocks.participation.postTestingEventsFeedbackObligationsFeedback,
    ).not.toHaveBeenCalled();
  });
});
