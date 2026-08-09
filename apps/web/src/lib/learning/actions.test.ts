import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getToken: vi.fn(),
  revalidatePath: vi.fn(),
  fetch: vi.fn(),
  resolveCourseId: vi.fn(),
  postCoursesContent: vi.fn(),
  postCoursesContentReorder: vi.fn(),
  deleteCoursesContent: vi.fn(),
  postAssessments: vi.fn(),
  putAssessments: vi.fn(),
  deleteAssessments: vi.fn(),
  getCourses1: vi.fn(),
  putCourses: vi.fn(),
  postCoursesPublish: vi.fn(),
  postCoursesRestore: vi.fn(),
  postCoursesUsers: vi.fn(),
  deleteCoursesUsers: vi.fn(),
  postApiLearningEnrollments: vi.fn(),
  clientRequest: vi.fn(),
  getUsers: vi.fn(),
  postCoursesStudentsMessage: vi.fn(),
  postCoursesSupportTicketsMessages: vi.fn(),
  postCoursesSupportTicketsResolve: vi.fn(),
  postApiCertificatesTemplates: vi.fn(),
  putApiCertificatesTemplates: vi.fn(),
  deleteApiCertificatesTemplates: vi.fn(),
  postAssessmentsGroups: vi.fn(),
  putAssessmentsGroups: vi.fn(),
  deleteAssessmentsGroups: vi.fn(),
  putAssessmentsDefinition: vi.fn(),
  patchApiSocialReviewsModeration: vi.fn(),
  postApiSocialDiscussions: vi.fn(),
  deleteApiSocialDiscussions: vi.fn(),
  postApiSocialDiscussionsPin: vi.fn(),
  postApiSocialDiscussionsUnpin: vi.fn(),
  postApiSocialDiscussionsResolve: vi.fn(),
  postApiSocialDiscussionsReplies: vi.fn(),
  postApiSocialRepliesAccept: vi.fn(),
  postApiSocialRepliesUpvote: vi.fn(),
  createServerClient: vi.fn(),
  getCourse: vi.fn(),
  getCourseContent: vi.fn(),
  deriveCourseLaunchSummary: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getToken: mocks.getToken,
}));

vi.mock("next/cache", () => ({
  revalidatePath: mocks.revalidatePath,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningAssessmentsModule: class {
      postAssessments = mocks.postAssessments;
      putAssessments = mocks.putAssessments;
      deleteAssessments = mocks.deleteAssessments;
      postAssessmentsGroups = mocks.postAssessmentsGroups;
      putAssessmentsGroups = mocks.putAssessmentsGroups;
      deleteAssessmentsGroups = mocks.deleteAssessmentsGroups;
      putAssessmentsDefinition = mocks.putAssessmentsDefinition;
    },
    LearningCoursesProgramModule: class {
      getCourses1 = mocks.getCourses1;
      putCourses = mocks.putCourses;
      postCoursesUsers = mocks.postCoursesUsers;
      deleteCoursesUsers = mocks.deleteCoursesUsers;
      postCoursesContentReorder = mocks.postCoursesContentReorder;
    },
    LearningCoursesProgramcontentModule: class {
      postCoursesContent = mocks.postCoursesContent;
      deleteCoursesContent = mocks.deleteCoursesContent;
    },
    LearningCoursesProgramlifecycleModule: class {
      postCoursesPublish = mocks.postCoursesPublish;
      postCoursesRestore = mocks.postCoursesRestore;
    },
    LearningEnrollmentsModule: class {
      postApiLearningEnrollments = mocks.postApiLearningEnrollments;
    },
    LearningCoursesStudentsModule: class {
      postCoursesStudentsMessage = mocks.postCoursesStudentsMessage;
    },
    LearningCoursesSupportticketsModule: class {
      postCoursesSupportTicketsMessages =
        mocks.postCoursesSupportTicketsMessages;
      postCoursesSupportTicketsResolve = mocks.postCoursesSupportTicketsResolve;
    },
    LearningCertificatesModule: class {
      postApiCertificatesTemplates = mocks.postApiCertificatesTemplates;
      putApiCertificatesTemplates = mocks.putApiCertificatesTemplates;
      deleteApiCertificatesTemplates = mocks.deleteApiCertificatesTemplates;
    },
    LearningExperienceSocialDiscussionsModule: class {
      postApiSocialDiscussions = mocks.postApiSocialDiscussions;
      deleteApiSocialDiscussions = mocks.deleteApiSocialDiscussions;
      postApiSocialDiscussionsPin = mocks.postApiSocialDiscussionsPin;
      postApiSocialDiscussionsUnpin = mocks.postApiSocialDiscussionsUnpin;
      postApiSocialDiscussionsResolve = mocks.postApiSocialDiscussionsResolve;
    },
    LearningExperienceSocialRepliesModule: class {
      postApiSocialDiscussionsReplies = mocks.postApiSocialDiscussionsReplies;
      postApiSocialRepliesAccept = mocks.postApiSocialRepliesAccept;
      postApiSocialRepliesUpvote = mocks.postApiSocialRepliesUpvote;
    },
    LearningExperienceSocialReviewsModule: class {
      patchApiSocialReviewsModeration = mocks.patchApiSocialReviewsModeration;
    },
    UsersModule: class {
      getUsers = mocks.getUsers;
    },
  },
}));

vi.mock("@/lib/learning/queries/course", () => ({
  resolveCourseId: mocks.resolveCourseId,
  getCourse: mocks.getCourse,
  getCourseContent: mocks.getCourseContent,
}));

vi.mock("@/lib/learning/course-launch", () => ({
  deriveCourseLaunchSummary: mocks.deriveCourseLaunchSummary,
}));

const {
  createCertificateTemplate,
  addContent,
  createAssessment,
  updateAssessmentDefinition,
  updateCertificateTemplate,
  deleteCertificateTemplate,
  deleteContent,
  reorderContent,
  createCourseDiscussion,
  createDiscussionReply,
  addCourseSupportTicketMessage,
  resolveCourseSupportTicket,
  deleteAssessmentGroup,
  updateDiscussionPin,
  updateAssessmentGroup,
  resolveDiscussion,
  updateCourseNotificationSettings,
  updateCourseIntegrationSettings,
  updateCourseReviewModeration,
  manualEnrollStudent,
  removeCourseStudents,
  sendCourseStudentMessage,
  transferCourseOwnership,
  updateCourse,
  publishCourse,
  restoreCourse,
} = await import("./actions");

describe("learning server actions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.resolveCourseId.mockImplementation(
      async (courseId: string) => courseId,
    );
    mocks.postCoursesContent.mockResolvedValue({
      ok: true,
      data: { id: "content-1" },
    });
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ id: "content-1" }), {
        status: 201,
        headers: { "Content-Type": "application/json" },
      }),
    );
    mocks.postCoursesContentReorder.mockResolvedValue({
      ok: true,
      data: undefined,
    });
    mocks.deleteCoursesContent.mockResolvedValue({ ok: true, data: undefined });
    mocks.postAssessments.mockResolvedValue({
      ok: true,
      data: { id: "assessment-1" },
    });
    mocks.putAssessments.mockResolvedValue({ ok: true, data: undefined });
    mocks.deleteAssessments.mockResolvedValue({ ok: true, data: undefined });
    mocks.getCourses1.mockResolvedValue({
      ok: true,
      data: {
        id: "course-1",
        metadata: JSON.stringify({ landingFaq: [{ question: "Existing" }] }),
      },
    });
    mocks.putCourses.mockResolvedValue({ ok: true, data: {} });
    mocks.postCoursesPublish.mockResolvedValue({ ok: true, data: {} });
    mocks.postCoursesRestore.mockResolvedValue({ ok: true, data: {} });
    mocks.createServerClient.mockReturnValue({ request: mocks.clientRequest });
    mocks.getCourse.mockResolvedValue({
      id: "course-1",
      title: "Ready course",
      description: "Ready course description.",
      slug: "ready-course",
      status: "draft",
      visibility: "public",
      thumbnail: "https://example.test/cover.jpg",
      enrollmentStatus: "Open",
      enrollmentDeadline: null,
      currentEnrollments: 0,
      isEnrollmentOpen: true,
    });
    mocks.getCourseContent.mockResolvedValue({ items: [], total: 0 });
    mocks.deriveCourseLaunchSummary.mockReturnValue({ blockers: [] });
    mocks.clientRequest.mockResolvedValue({
      ok: true,
      data: {
        items: [
          {
            id: "user-1",
            email: "student@example.com",
            username: "student",
            name: "Student",
          },
        ],
      },
    });
    mocks.getUsers.mockResolvedValue({
      ok: true,
      data: {
        items: [
          {
            id: "user-1",
            email: "student@example.com",
            username: "student",
            name: "Student",
          },
        ],
      },
    });
    mocks.postCoursesStudentsMessage.mockResolvedValue({
      ok: true,
      data: { sent: 2 },
    });
    mocks.postCoursesSupportTicketsMessages.mockResolvedValue({
      ok: true,
      data: { id: "ticket-1" },
    });
    mocks.postCoursesSupportTicketsResolve.mockResolvedValue({
      ok: true,
      data: { id: "ticket-1" },
    });
    mocks.postApiCertificatesTemplates.mockResolvedValue({
      ok: true,
      data: { id: "template-1" },
    });
    mocks.putApiCertificatesTemplates.mockResolvedValue({
      ok: true,
      data: { id: "template-1" },
    });
    mocks.deleteApiCertificatesTemplates.mockResolvedValue({
      ok: true,
      data: undefined,
    });
    mocks.postAssessmentsGroups.mockResolvedValue({
      ok: true,
      data: { id: "group-1" },
    });
    mocks.putAssessmentsGroups.mockResolvedValue({
      ok: true,
      data: { id: "group-1" },
    });
    mocks.deleteAssessmentsGroups.mockResolvedValue({
      ok: true,
      data: undefined,
    });
    mocks.putAssessmentsDefinition.mockResolvedValue({ ok: true, data: {} });
    mocks.patchApiSocialReviewsModeration.mockResolvedValue({
      ok: true,
      data: { id: "review-1" },
    });
    mocks.postApiSocialDiscussions.mockResolvedValue({
      ok: true,
      data: { id: "thread-1" },
    });
    mocks.deleteApiSocialDiscussions.mockResolvedValue({
      ok: true,
      data: undefined,
    });
    mocks.postApiSocialDiscussionsPin.mockResolvedValue({
      ok: true,
      data: { id: "thread-1" },
    });
    mocks.postApiSocialDiscussionsUnpin.mockResolvedValue({
      ok: true,
      data: { id: "thread-1" },
    });
    mocks.postApiSocialDiscussionsResolve.mockResolvedValue({
      ok: true,
      data: { id: "thread-1" },
    });
    mocks.postApiSocialDiscussionsReplies.mockResolvedValue({
      ok: true,
      data: { id: "reply-1" },
    });
    mocks.postApiSocialRepliesAccept.mockResolvedValue({
      ok: true,
      data: { id: "reply-1" },
    });
    mocks.postApiSocialRepliesUpvote.mockResolvedValue({
      ok: true,
      data: { id: "reply-1" },
    });
    mocks.postCoursesUsers.mockResolvedValue({
      ok: true,
      data: { enrollmentId: "program-user-1" },
    });
    mocks.deleteCoursesUsers.mockResolvedValue({ ok: true, data: undefined });
    mocks.postApiLearningEnrollments.mockResolvedValue({
      ok: true,
      data: { id: "cohort-enrollment-1" },
    });
    vi.stubGlobal("fetch", mocks.fetch);
  });

  it("uses explicit clear flags for nullable enrollment controls", async () => {
    const result = await updateCourse({
      courseId: "course-1",
      maxEnrollments: null,
      enrollmentDeadline: null,
    });

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.putCourses).toHaveBeenCalledWith("course-1", {
      clearMaxEnrollments: true,
      clearEnrollmentDeadline: true,
    });
  });

  it("preserves finite enrollment controls without clear flags", async () => {
    const result = await updateCourse({
      courseId: "course-1",
      maxEnrollments: 25,
      enrollmentDeadline: "2026-09-01T12:00:00.000Z",
    });

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.putCourses).toHaveBeenCalledWith("course-1", {
      maxEnrollments: 25,
      enrollmentDeadline: "2026-09-01T12:00:00.000Z",
    });
  });

  it("resolves canonical course routes before updating the API resource", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce("resolved-course-id");

    const result = await updateCourse({
      courseId: "boss-ai-by-instructor-one",
      title: "Resolved course",
    });

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.resolveCourseId).toHaveBeenCalledWith(
      "boss-ai-by-instructor-one",
    );
    expect(mocks.putCourses).toHaveBeenCalledWith("resolved-course-id", {
      title: "Resolved course",
    });
  });

  it("blocks publishing when course readiness is incomplete", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce("resolved-course-id");
    mocks.deriveCourseLaunchSummary.mockReturnValueOnce({
      blockers: ["Add at least one lesson", "Upload a cover image"],
    });

    const result = await publishCourse("boss-ai-by-instructor-one");

    expect(result).toEqual({
      success: false,
      error:
        "Course cannot be published until readiness is complete: Add at least one lesson, Upload a cover image.",
    });
    expect(mocks.getCourse).toHaveBeenCalledWith("boss-ai-by-instructor-one");
    expect(mocks.getCourseContent).toHaveBeenCalledWith("resolved-course-id");
    expect(mocks.postCoursesPublish).not.toHaveBeenCalled();
  });

  it("publishes only after readiness is complete and uses the canonical course id", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce("resolved-course-id");

    const result = await publishCourse("boss-ai-by-instructor-one");

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.postCoursesPublish).toHaveBeenCalledWith("resolved-course-id");
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/boss-ai-by-instructor-one",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/resolved-course-id",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/boss-ai-by-instructor-one/overview",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/resolved-course-id/overview",
    );
  });

  it("restores an archived course to draft using the canonical course id", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce("resolved-course-id");

    const result = await restoreCourse("boss-ai-by-instructor-one");

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.postCoursesRestore).toHaveBeenCalledWith("resolved-course-id");
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/boss-ai-by-instructor-one",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/resolved-course-id",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/boss-ai-by-instructor-one/overview",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/resolved-course-id/overview",
    );
  });

  it("persists notification settings while preserving unrelated course metadata", async () => {
    const result = await updateCourseNotificationSettings("course-1", {
      studentNotifications: {
        enrollmentConfirmation: true,
        courseUpdates: false,
        newContent: true,
        upcomingClasses: true,
        classReminders: [120, -1, 15, 120],
        assignmentDue: true,
        assessmentResults: true,
        certificateReady: true,
        discussionReplies: false,
      },
      instructorNotifications: {
        newEnrollment: true,
        newReview: true,
        supportTicket: true,
        discussionMention: false,
        lowRating: true,
        lowRatingThreshold: 9,
      },
      templates: [
        {
          id: "updates",
          type: "course-update",
          subject: "  Course update  ",
          enabled: true,
        },
      ],
    });

    expect(result).toEqual({ success: true, data: null });
    const metadata = JSON.parse(mocks.putCourses.mock.calls[0][1].metadata);
    expect(metadata.landingFaq).toEqual([{ question: "Existing" }]);
    expect(
      metadata.notificationSettings.studentNotifications.classReminders,
    ).toEqual([120, 15]);
    expect(
      metadata.notificationSettings.instructorNotifications.lowRatingThreshold,
    ).toBe(5);
    expect(metadata.notificationSettings.templates[0].subject).toBe(
      "Course update",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/settings/notifications",
    );
  });

  it("persists course integrations and rejects invalid webhook URLs", async () => {
    const invalid = await updateCourseIntegrationSettings("course-1", {
      integrations: [],
      webhooks: [
        {
          id: "hook-1",
          url: "javascript:alert(1)",
          events: ["course.updated"],
          enabled: true,
        },
      ],
    });
    expect(invalid).toEqual({
      success: false,
      error: "Webhook URLs must use http or https.",
    });

    const result = await updateCourseIntegrationSettings("course-1", {
      integrations: [
        {
          id: "discord",
          type: "discord",
          name: " Class Discord ",
          enabled: true,
          status: "connected",
          config: { inviteUrl: "https://discord.gg/gameguild" },
        },
      ],
      webhooks: [
        {
          id: "hook-1",
          url: "https://example.com/events",
          events: ["course.updated", "", "course.updated"],
          enabled: true,
        },
      ],
    });

    expect(result).toEqual({ success: true, data: null });
    const metadata = JSON.parse(
      mocks.putCourses.mock.calls.at(-1)?.[1].metadata,
    );
    expect(metadata.integrationSettings.integrations[0].name).toBe(
      "Class Discord",
    );
    expect(metadata.integrationSettings.webhooks[0].events).toEqual([
      "course.updated",
    ]);
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/settings/integrations",
    );
  });

  it("updates testimonial approval and featured state through the moderation endpoint", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(
        JSON.stringify({ id: "review-1", isApproved: true, isFeatured: false }),
        {
          status: 200,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    const result = await updateCourseReviewModeration(
      "course-1",
      "review-1",
      true,
      false,
    );

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.patchApiSocialReviewsModeration).toHaveBeenCalledWith(
      "review-1",
      {
        isApproved: true,
        isFeatured: false,
      },
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/listing/testimonials",
    );
  });

  it("enrolls through the canonical course roster and synchronizes an optional cohort", async () => {
    const result = await manualEnrollStudent({
      courseId: "course-slug",
      userId: "student@example.com",
      cohortId: "cohort-1",
    });

    expect(result).toEqual({ success: true, data: { id: "program-user-1" } });
    expect(mocks.postCoursesUsers).toHaveBeenCalledWith(
      "course-slug",
      "user-1",
    );
    expect(mocks.postApiLearningEnrollments).toHaveBeenCalledWith({
      courseId: "course-slug",
      userId: "user-1",
      cohortId: "cohort-1",
    });
  });

  it("rolls back the canonical roster when cohort synchronization fails", async () => {
    mocks.postApiLearningEnrollments.mockResolvedValueOnce({
      ok: false,
      error: { detail: "Cohort is full." },
    });

    const result = await manualEnrollStudent({
      courseId: "course-1",
      userId: "student@example.com",
      cohortId: "cohort-full",
    });

    expect(result).toEqual({ success: false, error: "Cohort is full." });
    expect(mocks.deleteCoursesUsers).toHaveBeenCalledWith("course-1", "user-1");
  });

  it("transfers ownership to a resolved user through the canonical course resource", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce("resolved-course-id");
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ id: "resolved-course-id" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await transferCourseOwnership(
      "boss-ai-by-instructor-one",
      "student@example.com",
    );

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.getUsers).toHaveBeenCalledWith({
      email: "student@example.com",
      limit: 5,
    });
    expect(mocks.putCourses).toHaveBeenCalledWith("resolved-course-id", {
      creatorId: "user-1",
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/boss-ai-by-instructor-one/settings/danger",
    );
  });

  it("removes selected users from the canonical course roster", async () => {
    const result = await removeCourseStudents("course-1", [
      "user-1",
      "user-2",
      "user-1",
    ]);

    expect(result).toEqual({ success: true, data: { removed: 2 } });
    expect(mocks.deleteCoursesUsers).toHaveBeenCalledTimes(2);
    expect(mocks.deleteCoursesUsers).toHaveBeenCalledWith("course-1", "user-1");
    expect(mocks.deleteCoursesUsers).toHaveBeenCalledWith("course-1", "user-2");
  });

  it("sends a course message to selected enrolled users", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ sent: 2 }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await sendCourseStudentMessage({
      courseId: "course-1",
      userIds: ["user-1", "user-2"],
      subject: " Milestone update ",
      message: " The critique session moved to Friday. ",
    });

    expect(result).toEqual({ success: true, data: { sent: 2 } });
    expect(mocks.postCoursesStudentsMessage).toHaveBeenCalledWith("course-1", {
      userIds: ["user-1", "user-2"],
      subject: "Milestone update",
      message: "The critique session moved to Friday.",
    });
  });

  it("replies to and resolves persisted course support tickets", async () => {
    mocks.fetch.mockImplementation(
      async () =>
        new Response(JSON.stringify({ id: "ticket-1" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
    );

    await expect(
      addCourseSupportTicketMessage({
        courseId: "course-1",
        ticketId: "ticket-1",
        message: " Please retry now. ",
      }),
    ).resolves.toEqual({ success: true, data: null });
    expect(mocks.postCoursesSupportTicketsMessages).toHaveBeenCalledWith(
      "course-1",
      "ticket-1",
      {
        message: "Please retry now.",
      },
    );

    await expect(
      resolveCourseSupportTicket({
        courseId: "course-1",
        ticketId: "ticket-1",
        summary: " Entitlement refreshed. ",
      }),
    ).resolves.toEqual({ success: true, data: null });
    expect(mocks.postCoursesSupportTicketsResolve).toHaveBeenCalledWith(
      "course-1",
      "ticket-1",
      {
        summary: "Entitlement refreshed.",
      },
    );
  });

  it("resolves dashboard course slugs before deleting course content", async () => {
    mocks.resolveCourseId.mockResolvedValue(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );

    const result = await deleteContent(
      "creature-design-by-admin",
      "9ec3b854-89ca-4757-83fb-cfc823da1a5e",
    );

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.resolveCourseId).toHaveBeenCalledWith(
      "creature-design-by-admin",
    );
    expect(mocks.deleteCoursesContent).toHaveBeenCalledWith(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
      "9ec3b854-89ca-4757-83fb-cfc823da1a5e",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/creature-design-by-admin",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );
  });

  it("revalidates the course content route after creating a lesson", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );

    const result = await addContent({
      courseId: "creature-design-by-admin",
      parentId: "9ec3b854-89ca-4757-83fb-cfc823da1a5e",
      title: "Gesture foundations",
      type: "Lesson",
    });

    expect(result).toEqual({ success: true, data: { id: "content-1" } });
    expect(mocks.postCoursesContent).toHaveBeenCalledWith(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
      expect.objectContaining({
        programId: "1caa16bb-6810-4e53-bb0d-91f0d5702333",
        parentId: "9ec3b854-89ca-4757-83fb-cfc823da1a5e",
        title: "Gesture foundations",
        type: "Lesson",
      }),
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/creature-design-by-admin/content",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/1caa16bb-6810-4e53-bb0d-91f0d5702333/content",
    );
  });

  it("uses the generated program contract to reorder course content", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );

    const result = await reorderContent("creature-design-by-admin", [
      "module-2",
      "module-1",
    ]);

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.postCoursesContentReorder).toHaveBeenCalledWith(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
      { contentIds: ["module-2", "module-1"] },
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/creature-design-by-admin/content",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/1caa16bb-6810-4e53-bb0d-91f0d5702333/content",
    );
  });

  it("revalidates the assessment hub after creating an assessment", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );

    const result = await createAssessment({
      courseId: "creature-design-by-admin",
      title: "Final review",
      type: "Quiz",
      maxScore: 100,
      passingScore: 70,
      assessmentGroupId: "group-1",
    });

    expect(result).toEqual({ success: true, data: { id: "assessment-1" } });
    expect(mocks.postAssessments).toHaveBeenCalledWith(
      expect.objectContaining({
        courseId: "1caa16bb-6810-4e53-bb0d-91f0d5702333",
        title: "Final review",
        type: "Quiz",
        assessmentGroupId: "group-1",
      }),
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/creature-design-by-admin/assessments",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/1caa16bb-6810-4e53-bb0d-91f0d5702333/assessments",
    );
  });

  it("saves authored assessment definitions through the assessments API", async () => {
    mocks.resolveCourseId.mockResolvedValueOnce(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );
    mocks.fetch.mockResolvedValue(
      new Response(
        JSON.stringify({
          assessmentId: "assessment-1",
          definitionSchemaVersion: 1,
          definition: { order: [], blocks: {} },
        }),
        {
          status: 200,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    const result = await updateAssessmentDefinition({
      courseId: "creature-design-by-admin",
      assessmentId: "assessment-1",
      definition: { order: [], blocks: {} },
    });

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.putAssessmentsDefinition).toHaveBeenCalledWith(
      "assessment-1",
      {
        definitionSchemaVersion: 1,
        definition: { order: [], blocks: {} },
      },
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/creature-design-by-admin/assessments",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/1caa16bb-6810-4e53-bb0d-91f0d5702333/assessments/assessment-1",
    );
  });

  it("creates certificate templates through the Learning.Certificates API", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(
        JSON.stringify({
          id: "template-1",
          courseId: "course-1",
          name: "Completion",
        }),
        {
          status: 201,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    const result = await createCertificateTemplate({
      courseId: "course-1",
      name: "Completion",
      templateHtml: "<section>{{recipientName}}</section>",
    });

    expect(result).toEqual({ success: true, data: { id: "template-1" } });
    expect(mocks.postApiCertificatesTemplates).toHaveBeenCalledWith({
      courseId: "course-1",
      name: "Completion",
      templateHtml: "<section>{{recipientName}}</section>",
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/certificates",
    );
  });

  it("deletes certificate templates and refreshes the certificate page", async () => {
    mocks.fetch.mockResolvedValue(new Response(null, { status: 204 }));

    const result = await deleteCertificateTemplate("course-1", "template-1");

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.deleteApiCertificatesTemplates).toHaveBeenCalledWith(
      "template-1",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/certificates",
    );
  });

  it("updates certificate templates and refreshes the list and editor", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(
        JSON.stringify({
          id: "template-1",
          courseId: "course-1",
          name: "Completion",
        }),
        {
          status: 200,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    const result = await updateCertificateTemplate({
      courseId: "course-1",
      templateId: "template-1",
      name: " Completion ",
      description: " Course credential ",
      templateHtml: "<main>{{recipientName}}</main>",
      templateStyles: " main { color: navy; } ",
      isDefault: true,
      isActive: true,
    });

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.putApiCertificatesTemplates).toHaveBeenCalledWith(
      "template-1",
      {
        name: "Completion",
        description: "Course credential",
        templateHtml: "<main>{{recipientName}}</main>",
        templateStyles: "main { color: navy; }",
        isDefault: true,
        isActive: true,
      },
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/certificates",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/certificates/template-1",
    );
  });

  it("creates course discussions through the Learning Social API", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ id: "thread-1", courseId: "course-1" }), {
        status: 201,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await createCourseDiscussion({
      courseId: "course-1",
      title: "Milestone review question",
      content: "Can I submit a revised prototype after review?",
    });

    expect(result).toEqual({ success: true, data: { id: "thread-1" } });
    expect(mocks.postApiSocialDiscussions).toHaveBeenCalledWith({
      courseId: "course-1",
      title: "Milestone review question",
      content: "Can I submit a revised prototype after review?",
      contentId: null,
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/support/discussions",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/support/discussions/thread-1",
    );
  });

  it("posts discussion replies and refreshes support routes", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(
        JSON.stringify({ id: "reply-1", discussionId: "thread-1" }),
        {
          status: 201,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    const result = await createDiscussionReply({
      courseId: "course-1",
      discussionId: "thread-1",
      content: "Yes, submit the revision before the checkpoint closes.",
    });

    expect(result).toEqual({ success: true, data: { id: "reply-1" } });
    expect(mocks.postApiSocialDiscussionsReplies).toHaveBeenCalledWith(
      "thread-1",
      {
        discussionId: "thread-1",
        content: "Yes, submit the revision before the checkpoint closes.",
        parentReplyId: null,
      },
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/support/tickets/thread-1",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/support/discussions/thread-1",
    );
  });

  it("pins and resolves discussion threads through social moderation endpoints", async () => {
    const okResponse = () =>
      new Response(JSON.stringify({ id: "thread-1", courseId: "course-1" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    mocks.fetch
      .mockResolvedValueOnce(okResponse())
      .mockResolvedValueOnce(okResponse());

    expect(await updateDiscussionPin("course-1", "thread-1", true)).toEqual({
      success: true,
      data: null,
    });
    expect(mocks.postApiSocialDiscussionsPin).toHaveBeenCalledWith("thread-1");

    expect(await resolveDiscussion("course-1", "thread-1")).toEqual({
      success: true,
      data: null,
    });
    expect(mocks.postApiSocialDiscussionsResolve).toHaveBeenCalledWith(
      "thread-1",
    );
  });

  it("updates assessment groups through the weighted grading endpoint", async () => {
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ id: "group-1", courseId: "course-1" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await updateAssessmentGroup({
      courseId: "course-1",
      groupId: "group-1",
      name: "Weekly quizzes",
      description: "Weekly knowledge checks.",
      weightPercent: 25,
      order: 2,
    });

    expect(result).toEqual({ success: true, data: { id: "group-1" } });
    expect(mocks.putAssessmentsGroups).toHaveBeenCalledWith("group-1", {
      name: "Weekly quizzes",
      description: "Weekly knowledge checks.",
      weightPercent: 25,
      order: 2,
    });
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/assessments",
    );
  });

  it("rejects invalid assessment group weights before calling the API", async () => {
    const result = await updateAssessmentGroup({
      courseId: "course-1",
      groupId: "group-1",
      name: "Weekly quizzes",
      weightPercent: 120,
    });

    expect(result).toEqual({
      success: false,
      error: "Weight must be between 0 and 100.",
    });
    expect(mocks.putAssessmentsGroups).not.toHaveBeenCalled();
  });

  it("deletes assessment groups and refreshes the assessment hub", async () => {
    mocks.fetch.mockResolvedValue(new Response(null, { status: 204 }));

    const result = await deleteAssessmentGroup("course-1", "group-1");

    expect(result).toEqual({ success: true, data: null });
    expect(mocks.deleteAssessmentsGroups).toHaveBeenCalledWith("group-1");
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1",
    );
    expect(mocks.revalidatePath).toHaveBeenCalledWith(
      "/dashboard/learning/courses/course-1/assessments",
    );
  });
});
