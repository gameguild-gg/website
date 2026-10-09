import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  createDiscussion: vi.fn(),
  createInteraction: vi.fn(),
  getInteraction: vi.fn(),
  getToken: vi.fn(),
  submitInteraction: vi.fn(),
  postReply: vi.fn(),
  getSubmissions: vi.fn(),
  startSubmission: vi.fn(),
  submitSubmission: vi.fn(),
  revalidatePath: vi.fn(),
}));

vi.mock("@/auth", () => ({ getToken: mocks.getToken }));
vi.mock("next/cache", () => ({ revalidatePath: mocks.revalidatePath }));
vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningExperienceSocialRepliesModule: class {
      postApiSocialDiscussionsReplies = mocks.postReply;
    },
    LearningExperienceSocialDiscussionsModule: class {
      postApiSocialDiscussions = mocks.createDiscussion;
    },
    LearningCoursesContentInteractionModule: class {
      getCourseInteractionsUserContent = mocks.getInteraction;
      postCourseInteractions = mocks.createInteraction;
      postCourseInteractionsSubmit = mocks.submitInteraction;
    },
    LearningAssessmentsModule: class {
      getAssessmentsMySubmissions = mocks.getSubmissions;
      postAssessmentsSubmissionsStart = mocks.startSubmission;
      postAssessmentsSubmissionsSubmit = mocks.submitSubmission;
    },
  },
}));

import {
  createCourseDiscussion,
  createCourseDiscussionReply,
  submitAssessment,
  submitContentActivity,
} from "./activity-actions";

function form(fields: Record<string, string | Blob | undefined>) {
  const result = new FormData();
  Object.entries(fields).forEach(([key, value]) => {
    if (value !== undefined) result.set(key, value);
  });
  return result;
}

function assessmentForm(
  overrides: Record<string, string | Blob | undefined> = {},
) {
  return form({
    assessmentId: "assessment-1",
    enrollmentId: "enrollment-1",
    modality: "Text",
    response: "A complete answer",
    ...overrides,
  });
}

function contentActivityForm(overrides: Record<string, string> = {}) {
  return form({
    courseId: "course-1",
    enrollmentId: "enrollment-1",
    contentId: "content-1",
    kind: "discussion",
    response: "A considered contribution",
    ...overrides,
  });
}

function discussionForm(overrides: Record<string, string> = {}) {
  return form({
    courseId: "course-1",
    courseSlug: "game-production",
    title: "Testing a game loop",
    content: "How should we validate this loop?",
    ...overrides,
  });
}

function replyForm(overrides: Record<string, string> = {}) {
  return form({
    discussionId: "discussion-1",
    courseSlug: "visual-storytelling-by-maya",
    content: "I would test the onboarding first.",
    parentReplyId: "",
    ...overrides,
  });
}

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe("learner discussion reply action", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "authenticated-client" });
    mocks.postReply.mockResolvedValue({ ok: true, data: { id: "reply-1" } });
  });

  it("validates the reply before authenticating", async () => {
    await expect(
      createCourseDiscussionReply(replyForm({ content: "   " })),
    ).resolves.toEqual({
      success: false,
      error: "A reply message is required.",
    });
    expect(mocks.getToken).not.toHaveBeenCalled();
    expect(mocks.postReply).not.toHaveBeenCalled();
  });

  it("requires an authenticated learner", async () => {
    mocks.getToken.mockResolvedValue(null);

    await expect(createCourseDiscussionReply(replyForm())).resolves.toEqual({
      success: false,
      error: "Your session expired. Sign in again.",
    });
    expect(mocks.postReply).not.toHaveBeenCalled();
  });

  it("publishes the reply and revalidates the list and thread routes", async () => {
    await expect(
      createCourseDiscussionReply(replyForm({ parentReplyId: "reply-parent" })),
    ).resolves.toEqual({ success: true });

    expect(mocks.postReply).toHaveBeenCalledWith("discussion-1", {
      discussionId: "discussion-1",
      content: "I would test the onboarding first.",
      parentReplyId: "reply-parent",
    });
    expect(mocks.revalidatePath).toHaveBeenNthCalledWith(
      1,
      "/learn/courses/visual-storytelling-by-maya/community",
    );
    expect(mocks.revalidatePath).toHaveBeenNthCalledWith(
      2,
      "/learn/courses/visual-storytelling-by-maya/community/discussion-1",
    );
  });

  it("returns the API detail and does not revalidate on failure", async () => {
    mocks.postReply.mockResolvedValue({
      ok: false,
      error: { detail: "Replies are closed." },
    });

    await expect(createCourseDiscussionReply(replyForm())).resolves.toEqual({
      success: false,
      error: "Replies are closed.",
    });
    expect(mocks.revalidatePath).not.toHaveBeenCalled();
  });

  it("validates a missing discussion id before authenticating", async () => {
    await expect(
      createCourseDiscussionReply(replyForm({ discussionId: "" })),
    ).resolves.toEqual({
      success: false,
      error: "A reply message is required.",
    });
    expect(mocks.getToken).not.toHaveBeenCalled();
  });

  it("publishes a root reply with a null parent", async () => {
    await expect(createCourseDiscussionReply(replyForm())).resolves.toEqual({
      success: true,
    });
    expect(mocks.postReply).toHaveBeenCalledWith(
      "discussion-1",
      expect.objectContaining({ parentReplyId: null }),
    );
  });

  it("returns a safe reply error when the request throws", async () => {
    mocks.postReply.mockRejectedValue("network down");

    await expect(createCourseDiscussionReply(replyForm())).resolves.toEqual({
      success: false,
      error: "The reply could not be published.",
    });
  });

  it("falls back when a reply API error only contains blank fields", async () => {
    mocks.postReply.mockResolvedValue({
      ok: false,
      error: { detail: "", message: "", title: " " },
    });

    await expect(createCourseDiscussionReply(replyForm())).resolves.toEqual({
      success: false,
      error: "The reply could not be published.",
    });
  });

  it("allows a reply without a course slug and revalidates the generated routes", async () => {
    await expect(
      createCourseDiscussionReply(replyForm({ courseSlug: "" })),
    ).resolves.toEqual({ success: true });
    expect(mocks.revalidatePath).toHaveBeenNthCalledWith(
      1,
      "/learn/courses//community",
    );
  });

  it("validates a completely missing reply body", async () => {
    const missingBody = replyForm();
    missingBody.delete("content");

    await expect(createCourseDiscussionReply(missingBody)).resolves.toEqual({
      success: false,
      error: "A reply message is required.",
    });
  });
});

describe("learner course discussion action", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "authenticated-client" });
    mocks.createDiscussion.mockResolvedValue({
      ok: true,
      data: { id: "discussion-1" },
    });
  });

  it.each([
    ["courseId", ""],
    ["title", "   "],
    ["content", "   "],
  ])("validates a missing %s before authenticating", async (key, value) => {
    await expect(
      createCourseDiscussion(discussionForm({ [key]: value })),
    ).resolves.toEqual({
      success: false,
      error: "Title and message are required.",
    });
    expect(mocks.getToken).not.toHaveBeenCalled();
  });

  it("requires an authenticated discussion author", async () => {
    mocks.getToken.mockResolvedValue(null);

    await expect(createCourseDiscussion(discussionForm())).resolves.toEqual({
      success: false,
      error: "Your session expired. Sign in again.",
    });
  });

  it("creates a trimmed discussion and revalidates its course community", async () => {
    await expect(
      createCourseDiscussion(
        discussionForm({
          title: "  Testing a game loop  ",
          content: "  How should we validate this loop?  ",
        }),
      ),
    ).resolves.toEqual({ success: true });

    expect(mocks.createDiscussion).toHaveBeenCalledWith({
      courseId: "course-1",
      title: "Testing a game loop",
      content: "How should we validate this loop?",
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/learn/courses/game-production/community",
    );
  });

  it("accepts a missing course slug without changing the submitted discussion", async () => {
    const missingSlug = discussionForm();
    missingSlug.delete("courseSlug");

    await expect(createCourseDiscussion(missingSlug)).resolves.toEqual({
      success: true,
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/learn/courses//community",
    );
  });

  it.each(["title", "content"])(
    "validates a completely missing %s",
    async (key) => {
      const missingField = discussionForm();
      missingField.delete(key);

      await expect(createCourseDiscussion(missingField)).resolves.toEqual({
        success: false,
        error: "Title and message are required.",
      });
    },
  );

  it("returns the API title when discussion creation fails", async () => {
    mocks.createDiscussion.mockResolvedValue({
      ok: false,
      error: { title: "Discussion creation is disabled." },
    });

    await expect(createCourseDiscussion(discussionForm())).resolves.toEqual({
      success: false,
      error: "Discussion creation is disabled.",
    });
    expect(mocks.revalidatePath).not.toHaveBeenCalled();
  });

  it("returns a safe discussion error when the request throws", async () => {
    mocks.createDiscussion.mockRejectedValue(null);

    await expect(createCourseDiscussion(discussionForm())).resolves.toEqual({
      success: false,
      error: "The discussion could not be created.",
    });
  });
});

describe("learner content activity action", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "authenticated-client" });
    mocks.getInteraction.mockResolvedValue({
      ok: true,
      data: { id: "interaction-1" },
    });
    mocks.createInteraction.mockResolvedValue({
      ok: true,
      data: { id: "interaction-created" },
    });
    mocks.submitInteraction.mockResolvedValue({ ok: true, data: {} });
  });

  it.each([
    ["courseId", ""],
    ["enrollmentId", ""],
    ["contentId", ""],
    ["kind", "unsupported"],
  ])("validates invalid activity %s context", async (key, value) => {
    await expect(
      submitContentActivity(
        { success: false },
        contentActivityForm({ [key]: value }),
      ),
    ).resolves.toEqual({
      success: false,
      error: "Activity enrollment context is missing.",
    });
    expect(mocks.getToken).not.toHaveBeenCalled();
  });

  it("validates completely missing activity kind and response values", async () => {
    const missingKind = contentActivityForm();
    missingKind.delete("kind");
    await expect(
      submitContentActivity({ success: false }, missingKind),
    ).resolves.toEqual({
      success: false,
      error: "Activity enrollment context is missing.",
    });

    const missingResponse = contentActivityForm();
    missingResponse.delete("response");
    await expect(
      submitContentActivity({ success: false }, missingResponse),
    ).resolves.toEqual({
      success: false,
      error: "A response is required.",
    });
  });

  it("requires an authenticated activity participant", async () => {
    mocks.getToken.mockResolvedValue(null);

    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({
      success: false,
      error: "Your session expired. Sign in again.",
    });
  });

  it.each([
    ["discussion", { kind: "discussion", body: "A considered contribution" }],
    ["reflection", { kind: "reflection", body: "A considered contribution" }],
    [
      "survey",
      {
        kind: "survey",
        answers: { response: "A considered contribution" },
      },
    ],
  ] as const)("submits an existing %s interaction", async (kind, payload) => {
    await expect(
      submitContentActivity({ success: false }, contentActivityForm({ kind })),
    ).resolves.toEqual({ success: true });

    expect(mocks.getInteraction).toHaveBeenCalledWith(
      "enrollment-1",
      "content-1",
      { programId: "course-1" },
    );
    expect(mocks.createInteraction).not.toHaveBeenCalled();
    expect(mocks.submitInteraction).toHaveBeenCalledWith(
      "interaction-1",
      {
        contentId: "content-1",
        programUserId: "enrollment-1",
        submissionData: JSON.stringify(payload),
      },
      { programId: "course-1" },
    );
  });

  it("creates an interaction when the content has not been started", async () => {
    mocks.getInteraction.mockResolvedValue({
      ok: false,
      error: { status: 404 },
    });

    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({ success: true });
    expect(mocks.createInteraction).toHaveBeenCalledWith(
      { contentId: "content-1", programUserId: "enrollment-1" },
      { programId: "course-1" },
    );
    expect(mocks.submitInteraction).toHaveBeenCalledWith(
      "interaction-created",
      expect.any(Object),
      { programId: "course-1" },
    );
  });

  it("reports failed and unidentified interaction starts", async () => {
    mocks.getInteraction
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, data: {} });
    mocks.createInteraction.mockResolvedValue({ ok: false });

    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({
      success: false,
      error: "The activity could not be started.",
    });
    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({
      success: false,
      error: "The activity could not be started.",
    });
  });

  it("returns the API message when activity submission fails", async () => {
    mocks.submitInteraction.mockResolvedValue({
      ok: false,
      error: { message: "The response window is closed." },
    });

    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({
      success: false,
      error: "The response window is closed.",
    });
  });

  it("returns a safe activity error when submission throws", async () => {
    mocks.getInteraction.mockRejectedValue(42);

    await expect(
      submitContentActivity({ success: false }, contentActivityForm()),
    ).resolves.toEqual({
      success: false,
      error: "The activity response could not be submitted.",
    });
  });
});

describe("learner assessment file action", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "authenticated-client" });
    mocks.getSubmissions.mockResolvedValue({ ok: true, data: [] });
    mocks.startSubmission.mockResolvedValue({
      ok: true,
      data: { submission: { id: "submission-1" } },
    });
    mocks.submitSubmission.mockResolvedValue({ ok: true, data: {} });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ assetReferenceId: "asset-1" }), {
          status: 201,
          headers: { "content-type": "application/json" },
        }),
      ),
    );
  });

  it.each([
    ["assessmentId", ""],
    ["enrollmentId", ""],
  ])("validates missing %s context", async (key, value) => {
    await expect(
      submitAssessment({ success: false }, assessmentForm({ [key]: value })),
    ).resolves.toEqual({
      success: false,
      error: "Assessment enrollment context is missing.",
    });
    expect(mocks.getToken).not.toHaveBeenCalled();
  });

  it("requires an authenticated assessment participant", async () => {
    mocks.getToken.mockResolvedValue(null);

    await expect(
      submitAssessment({ success: false }, assessmentForm()),
    ).resolves.toEqual({
      success: false,
      error: "Your session expired. Sign in again.",
    });
  });

  it("reuses the matching in-progress attempt and defaults to text", async () => {
    mocks.getSubmissions.mockResolvedValue({
      ok: true,
      data: [
        {
          id: "different-assessment",
          assessmentId: "assessment-2",
          status: "InProgress",
        },
        {
          id: "completed-attempt",
          assessmentId: "assessment-1",
          status: "Submitted",
        },
        {
          id: "submission-current",
          assessmentId: "assessment-1",
          status: "InProgress",
        },
      ],
    });

    await expect(
      submitAssessment(
        { success: false },
        assessmentForm({ modality: undefined }),
      ),
    ).resolves.toEqual({ success: true });

    expect(mocks.startSubmission).not.toHaveBeenCalled();
    expect(mocks.submitSubmission).toHaveBeenCalledWith("submission-current", {
      textPayload: "A complete answer",
    });
  });

  it("starts an attempt when submission history is unavailable", async () => {
    mocks.getSubmissions.mockResolvedValue({
      ok: false,
      error: { status: 503 },
    });

    await expect(
      submitAssessment({ success: false }, assessmentForm()),
    ).resolves.toEqual({ success: true });
    expect(mocks.startSubmission).toHaveBeenCalledWith("assessment-1", {
      enrollmentId: "enrollment-1",
    });
  });

  it("returns the start error detail when an attempt cannot be created", async () => {
    mocks.startSubmission.mockResolvedValue({
      ok: false,
      error: { detail: "No attempts remain." },
    });

    await expect(
      submitAssessment({ success: false }, assessmentForm()),
    ).resolves.toEqual({
      success: false,
      error: "The assessment attempt could not be started: No attempts remain.",
    });
  });

  it("rejects a started attempt without an identifier", async () => {
    mocks.startSubmission.mockResolvedValue({
      ok: true,
      data: { submission: {} },
    });

    await expect(
      submitAssessment({ success: false }, assessmentForm()),
    ).resolves.toEqual({
      success: false,
      error: "The assessment attempt did not return an identifier.",
    });
  });

  it("returns a structured API error when assessment submission fails", async () => {
    mocks.submitSubmission.mockResolvedValue({
      ok: false,
      error: {
        detail: "   ",
        message: "",
        title: "Assessment submission is closed.",
      },
    });

    await expect(
      submitAssessment({ success: false }, assessmentForm()),
    ).resolves.toEqual({
      success: false,
      error:
        "The assessment response could not be submitted: Assessment submission is closed.",
    });
  });

  it("validates an empty assessment response", async () => {
    await expect(
      submitAssessment({ success: false }, assessmentForm({ response: "   " })),
    ).resolves.toEqual({
      success: false,
      error: "A submission response is required.",
    });
    expect(mocks.submitSubmission).not.toHaveBeenCalled();
  });

  it("validates a completely missing assessment response", async () => {
    const missingResponse = assessmentForm();
    missingResponse.delete("response");

    await expect(
      submitAssessment({ success: false }, missingResponse),
    ).resolves.toEqual({
      success: false,
      error: "A submission response is required.",
    });
  });

  it("rejects an empty file before uploading it", async () => {
    await expect(
      submitAssessment(
        { success: false },
        assessmentForm({
          modality: "File",
          file: new File([], "empty.txt", { type: "text/plain" }),
        }),
      ),
    ).resolves.toEqual({
      success: false,
      error: "Choose a file before submitting.",
    });
    expect(fetch).not.toHaveBeenCalled();
  });

  it.each([
    [
      new Response(JSON.stringify({ detail: "File type is blocked." }), {
        status: 400,
        headers: { "content-type": "application/json" },
      }),
      "File type is blocked.",
    ],
    [
      new Response(JSON.stringify({ title: "Asset was rejected." }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
      "Asset was rejected.",
    ],
    [
      new Response("not-json", { status: 500 }),
      "The file could not be uploaded.",
    ],
  ])("returns a useful asset upload failure", async (response, error) => {
    vi.mocked(fetch).mockResolvedValueOnce(response);

    await expect(
      submitAssessment(
        { success: false },
        assessmentForm({
          modality: "File",
          file: new File(["answer"], "answer.txt", { type: "text/plain" }),
        }),
      ),
    ).resolves.toEqual({ success: false, error });
    expect(mocks.submitSubmission).not.toHaveBeenCalled();
  });

  it("parents the uploaded file to the concrete submission", async () => {
    const form = new FormData();
    form.set("assessmentId", "assessment-1");
    form.set("enrollmentId", "enrollment-1");
    form.set("modality", "File");
    form.set(
      "file",
      new File(["submission"], "answer.txt", { type: "text/plain" }),
    );

    await expect(submitAssessment({ success: false }, form)).resolves.toEqual({
      success: true,
    });

    const endpoint = (vi.mocked(fetch).mock.calls[0]?.[0] as URL).searchParams;
    expect(endpoint.get("parentResourceType")).toBe("AssessmentSubmission");
    expect(endpoint.get("parentResourceId")).toBe("submission-1");
    expect(endpoint.get("accessPolicy")).toBe("Private");
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.any(URL),
      expect.objectContaining({
        method: "POST",
        headers: { Authorization: "Bearer access-token" },
        cache: "no-store",
        redirect: "error",
      }),
    );
    expect(mocks.submitSubmission).toHaveBeenCalledWith("submission-1", {
      filePayload: "asset-1",
    });
  });
});

describe("learner action API configuration", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "authenticated-client" });
    mocks.createDiscussion.mockResolvedValue({ ok: true, data: {} });
  });

  it("prefers the server API URL and binds the authenticated token", async () => {
    vi.stubEnv("API_URL", "https://server-api.example");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "https://public-api.example");

    await createCourseDiscussion(discussionForm());

    const options = mocks.createServerClient.mock.calls[0]?.[0];
    expect(options.baseUrl).toBe("https://server-api.example");
    await expect(options.auth.getAccessToken()).resolves.toBe("access-token");
  });

  it("uses the public API URL when no server URL is configured", async () => {
    vi.stubEnv("API_URL", "");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "https://public-api.example");

    await createCourseDiscussion(discussionForm());

    expect(mocks.createServerClient).toHaveBeenCalledWith(
      expect.objectContaining({ baseUrl: "https://public-api.example" }),
    );
  });

  it("uses the local API URL when no environment URL is configured", async () => {
    vi.stubEnv("API_URL", "");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "");

    await createCourseDiscussion(discussionForm());

    expect(mocks.createServerClient).toHaveBeenCalledWith(
      expect.objectContaining({ baseUrl: "http://localhost:8080" }),
    );
  });
});
