import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import type React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  redirect: vi.fn((href: string) => {
    throw new Error(`redirect:${href}`);
  }),
  notFound: vi.fn(() => {
    throw new Error("not-found");
  }),
  getCourse: vi.fn(),
  getCourseAnalytics: vi.fn(),
  getCourseCompletionAnalytics: vi.fn(),
  getCourseEngagementAnalytics: vi.fn(),
  getCourseRevenueAnalytics: vi.fn(),
  getCourseSupportTickets: vi.fn(),
  getSupportTicket: vi.fn(),
  getCourseDiscussions: vi.fn(),
  getDiscussionThread: vi.fn(),
  getCourseNotificationSettings: vi.fn(),
  getCourseIntegrationSettings: vi.fn(),
  getCourseFaq: vi.fn(),
  getCoursePricing: vi.fn(),
  getCourseLandingProjects: vi.fn(),
  getCourseTestimonials: vi.fn(),
  getCourseContent: vi.fn(),
  getContentItem: vi.fn(),
  getAuthoringDraft: vi.fn(),
  getAssessment: vi.fn(),
  getCourseAssessments: vi.fn(),
  getCourseAssessmentGroups: vi.fn(),
  getCourseCertificates: vi.fn(),
  getCertificateTemplate: vi.fn(),
  getCourseGroupSets: vi.fn(),
  getAssessmentRubric: vi.fn(),
  canManageCourse: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  usePathname: () => '/workspace/learning',
  redirect: mocks.redirect,
  notFound: mocks.notFound,
}));

vi.mock("@/i18n/navigation", () => ({
  Link: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/lib/learning/course-route", () => ({
  getCourseRouteParam: (course: { slug?: string | null; id: string }) =>
    course.slug || course.id,
}));

vi.mock("@/lib/learning", () => ({
  getCourse: mocks.getCourse,
  getCourseAnalytics: mocks.getCourseAnalytics,
  getCourseCompletionAnalytics: mocks.getCourseCompletionAnalytics,
  getCourseEngagementAnalytics: mocks.getCourseEngagementAnalytics,
  getCourseRevenueAnalytics: mocks.getCourseRevenueAnalytics,
  getCourseSupportTickets: mocks.getCourseSupportTickets,
  getSupportTicket: mocks.getSupportTicket,
  getCourseDiscussions: mocks.getCourseDiscussions,
  getDiscussionThread: mocks.getDiscussionThread,
  getCourseNotificationSettings: mocks.getCourseNotificationSettings,
  getCourseIntegrationSettings: mocks.getCourseIntegrationSettings,
  getCourseFaq: mocks.getCourseFaq,
  getCoursePricing: mocks.getCoursePricing,
  getCourseLandingProjects: mocks.getCourseLandingProjects,
  getCourseTestimonials: mocks.getCourseTestimonials,
  getCourseContent: mocks.getCourseContent,
  getContentItem: mocks.getContentItem,
  getAssessment: mocks.getAssessment,
  getCourseAssessments: mocks.getCourseAssessments,
  getCourseAssessmentGroups: mocks.getCourseAssessmentGroups,
  getCourseCertificates: mocks.getCourseCertificates,
  getCertificateTemplate: mocks.getCertificateTemplate,
  getCourseGroupSets: mocks.getCourseGroupSets,
  getAssessmentRubric: mocks.getAssessmentRubric,
  canManageCourse: mocks.canManageCourse,
}));

vi.mock("@/lib/learning/authoring", () => ({
  getAuthoringDraft: mocks.getAuthoringDraft,
}));

vi.mock("@/components/learning/authoring/lesson-authoring-workspace", () => ({
  LessonAuthoringWorkspace: ({
    courseTitle,
    item,
    initialDraft,
  }: {
    courseTitle: string;
    item: { title: string };
    initialDraft: { revision: number };
  }) => (
    <div data-testid="lesson-authoring-workspace">
      {`${courseTitle}:${item.title}:${initialDraft.revision}`}
    </div>
  ),
}));

vi.mock("./assessments/[assessmentId]/assessment-editor", () => ({
  AssessmentEditor: ({
    assessment,
    assessmentGroups,
  }: {
    assessment: { title: string };
    assessmentGroups: unknown[];
  }) => (
    <div data-testid="assessment-editor">{`${assessment.title}:${assessmentGroups.length}`}</div>
  ),
}));

vi.mock("./listing/faq/faq-editor-form", () => ({
  FaqEditorForm: ({ items }: { items: unknown[] }) => (
    <div data-testid="faq-editor">{`${items.length} faq items`}</div>
  ),
}));

vi.mock("./listing/pricing/pricing-editor-form", () => ({
  PricingEditorForm: ({ pricing }: { pricing: { refundPolicy: string } }) => (
    <div data-testid="pricing-editor">{pricing.refundPolicy}</div>
  ),
}));

vi.mock("./listing/projects/project-carousel-editor-form", () => ({
  ProjectCarouselEditorForm: ({ items }: { items: unknown[] }) => (
    <div data-testid="project-carousel-editor">{`${items.length} project slides`}</div>
  ),
}));

vi.mock("./support/discussions/course-discussions-manager", () => ({
  CourseDiscussionsManager: ({
    courseTitle,
    threads,
  }: {
    courseTitle: string;
    threads: unknown[];
  }) => (
    <div data-testid="course-discussions-manager">{`${courseTitle}:${threads.length}`}</div>
  ),
}));

vi.mock("./support/thread-action-panel", () => ({
  ThreadActionPanel: ({
    threadId,
    replies,
  }: {
    threadId: string;
    replies: unknown[];
  }) => (
    <aside data-testid="thread-action-panel">{`${threadId}:${replies.length}`}</aside>
  ),
}));

vi.mock("./support/tickets/course-ticket-action-panel", () => ({
  CourseTicketActionPanel: ({
    ticketId,
    resolved,
  }: {
    ticketId: string;
    resolved: boolean;
  }) => (
    <aside data-testid="course-ticket-action-panel">{`${ticketId}:${resolved}`}</aside>
  ),
}));

import AnalyticsLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/analytics/layout";
import AnalyticsRedirectPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/analytics/page";
import CompletionAnalyticsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/analytics/completion/page";
import EngagementAnalyticsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/analytics/engagement/page";
import RevenueAnalyticsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/analytics/revenue/page";
import AssessmentsLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/assessments/layout";
import AssessmentDetailPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/assessments/[assessmentSlug]/page";
import CertificatesLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/certificates/layout";
import CertificateTemplateDetailPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/certificates/[templateId]/page";
import ContentItemPage from "@/app/[locale]/(authoring)/workspace/learning/courses/[course]/content/[contentSlug]/page";
import ListingLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/layout";
import ListingFaqPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/faq/page";
import ListingPricingPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/pricing/page";
import ListingProjectsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/projects/page";
import ListingTestimonialsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/testimonials/page";
import SettingsLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/settings/layout";
import SettingsRedirectPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/settings/page";
import GeneralSettingsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/settings/general/page";
import IntegrationSettingsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/settings/integrations/page";
import NotificationSettingsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/settings/notifications/page";
import SupportLayout from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/layout";
import SupportRedirectPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/page";
import DiscussionsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/discussions/page";
import DiscussionThreadPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/discussions/[threadId]/page";
import SupportTicketsPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/tickets/page";
import SupportTicketDetailPage from "@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/support/tickets/[ticketId]/page";

const course = {
  id: "course-1",
  slug: "advanced-ai-by-gameguild",
  title: "Advanced AI",
  description: "Advanced course",
  status: "Published",
  features: {
    hasPricing: true,
    hasDiscussions: true,
    hasAssessments: true,
    hasCertificate: true,
  },
};

const params = (extra: Record<string, string> = {}) =>
  Promise.resolve({
    locale: "en-US",
    course: "course-1",
    ...extra,
  });

describe("course-management secondary route pages", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getCourse.mockResolvedValue(course);
    mocks.getCourseAnalytics.mockResolvedValue({
      totalUsers: 20,
      completedUsers: 8,
      completionRate: 40,
    });
    mocks.getCourseCompletionAnalytics.mockResolvedValue({
      totalEnrolled: 20,
      totalCompleted: 8,
      completionRate: 40,
      funnel: [
        { stage: "Started", count: 20, percentage: 100 },
        { stage: "Completed", count: 8, percentage: 40 },
      ],
    });
    mocks.getCourseEngagementAnalytics.mockResolvedValue({
      activeStudents: 12,
      totalViews: 1200,
      avgSessionDuration: 5400,
      dailyActivity: [{ date: "2026-07-01", activeUsers: 7 }],
      peakHours: [{ hour: 14, activity: 9 }],
      contentViews: [
        {
          contentTitle: "AI Navigation",
          views: 300,
          avgWatchTime: 900,
          completionRate: 80,
        },
      ],
    });
    mocks.getCourseRevenueAnalytics.mockResolvedValue({
      currency: "USD",
      totalRevenue: 1500,
      totalTransactions: 10,
      avgTransactionValue: 150,
      refundRate: 2,
      revenueByTier: [
        { tierId: "tier-1", tierName: "Premium", revenue: 1500, count: 10 },
      ],
    });
    mocks.getCourseSupportTickets.mockResolvedValue({
      openCount: 1,
      inProgressCount: 1,
      resolvedCount: 2,
      tickets: [
        {
          id: "ticket-1",
          subject: "Cannot submit quiz",
          studentName: "Ada",
          messageCount: 3,
          status: "open",
        },
      ],
    });
    mocks.getSupportTicket.mockResolvedValue({
      id: "ticket-1",
      subject: "Cannot submit quiz",
      status: "open",
      priority: "high",
      category: "assessment",
      messages: [
        {
          id: "m1",
          authorId: "student-1",
          authorName: "Ada",
          authorRole: "student",
          content: "The quiz button is disabled.",
          createdAt: "2026-07-01T10:00:00.000Z",
        },
        {
          id: "m2",
          authorId: "support-1",
          authorName: "Support",
          authorRole: "support",
          content: "We are checking it.",
          createdAt: "2026-07-01T10:05:00.000Z",
        },
      ],
    });
    mocks.getCourseDiscussions.mockResolvedValue({
      threads: [{ id: "thread-1", title: "Week 1 help" }],
    });
    mocks.getDiscussionThread.mockResolvedValue({
      id: "thread-1",
      title: "Week 1 help",
      authorName: "Ada",
      content: "How do I start?",
      locked: true,
      pinned: true,
      replyCount: 1,
      viewCount: 12,
      createdAt: "2026-07-01T09:00:00.000Z",
      replies: [
        {
          id: "reply-1",
          threadId: "thread-1",
          authorId: "teacher-1",
          authorName: "Teacher",
          authorRole: "instructor",
          content: "Start with lesson one.",
          upvotes: 2,
          isAnswer: true,
          createdAt: "2026-07-01T09:30:00.000Z",
          updatedAt: "2026-07-01T09:30:00.000Z",
        },
      ],
    });
    mocks.getCourseNotificationSettings.mockResolvedValue({
      courseId: "course-1",
      studentNotifications: {
        enrollmentConfirmation: true,
        courseUpdates: true,
        newContent: true,
        upcomingClasses: true,
        classReminders: [60],
        assignmentDue: true,
        assessmentResults: true,
        certificateReady: true,
        discussionReplies: true,
      },
      instructorNotifications: {
        newEnrollment: true,
        newReview: true,
        supportTicket: true,
        discussionMention: true,
        lowRating: true,
        lowRatingThreshold: 3,
      },
      templates: [
        {
          id: "template-1",
          subject: "Welcome",
          type: "Enrollment",
          enabled: true,
        },
      ],
      updatedAt: "2026-07-01T00:00:00.000Z",
    });
    mocks.getCourseIntegrationSettings.mockResolvedValue({
      courseId: "course-1",
      integrations: [
        {
          id: "int-1",
          name: "Discord",
          type: "discord",
          enabled: true,
          config: {},
          status: "connected",
        },
      ],
      webhooks: [],
      updatedAt: "2026-07-01T00:00:00.000Z",
    });
    mocks.getCourseFaq.mockResolvedValue({
      items: [{ id: "faq-1", question: "When?", answer: "Now." }],
    });
    mocks.getCoursePricing.mockResolvedValue({
      refundPolicy: "Refunds available for 14 days.",
      tiers: [],
      discounts: [],
      hasFreeTrial: false,
    });
    mocks.getCourseLandingProjects.mockResolvedValue({
      items: [{ id: "project-1", title: "Boss AI" }],
    });
    mocks.getCourseTestimonials.mockResolvedValue({
      total: 1,
      averageRating: 4.5,
      testimonials: [
        {
          id: "review-1",
          title: "Useful",
          rating: 5,
          content: "Great",
          studentName: "Ada",
        },
      ],
    });
    mocks.getCourseContent.mockResolvedValue({ items: [], total: 0 });
    mocks.getContentItem.mockResolvedValue({
      id: "content-1",
      title: "Lesson 1",
    });
    mocks.getAuthoringDraft.mockResolvedValue({
      success: true,
      data: { revision: 2 },
    });
    mocks.getAssessment.mockResolvedValue({
      id: "assessment-1",
      title: "Quiz 1",
    });
    mocks.getCourseAssessments.mockResolvedValue({
      assessments: [{ id: "assessment-1", title: "Quiz 1" }],
      total: 1,
    });
    mocks.getCourseAssessmentGroups.mockResolvedValue([
      { id: "group-1", name: "Quizzes" },
    ]);
    mocks.getCourseCertificates.mockResolvedValue({
      templates: [{ id: "template-1", name: "Completion certificate" }],
      total: 1,
    });
    mocks.getCourseGroupSets.mockResolvedValue([]);
    mocks.getAssessmentRubric.mockResolvedValue({
      rubric: null,
      locked: false,
    });
    mocks.canManageCourse.mockResolvedValue(false);
    mocks.getCertificateTemplate.mockResolvedValue({
      id: "template-1",
      courseId: "course-1",
      name: "Completion certificate",
      description: "Issued on completion",
      status: "active",
      isDefault: true,
      issuedCount: 3,
      createdAt: "2026-06-01T00:00:00.000Z",
      templateHtml: "<p>Certificate body</p>",
      templateStyles: null,
      previewUrl: "/api/certificates/templates/template-1",
      updatedAt: "2026-07-01T00:00:00.000Z",
    });
  });

  it("redirects index routes to their canonical course-management tabs", async () => {
    await expect(
      AnalyticsRedirectPage({ params: params() } as never),
    ).rejects.toThrow(
      "redirect:/en-US/workspace/learning/courses/advanced-ai-by-gameguild/overview",
    );
    await expect(
      SupportRedirectPage({ params: params() } as never),
    ).rejects.toThrow(
      "redirect:/en-US/workspace/learning/courses/advanced-ai-by-gameguild/support/tickets",
    );
    await expect(
      SettingsRedirectPage({ params: params() } as never),
    ).rejects.toThrow(
      "redirect:/en-US/workspace/learning/courses/advanced-ai-by-gameguild/settings/danger",
    );
    await expect(
      GeneralSettingsPage({ params: params() } as never),
    ).rejects.toThrow(
      "redirect:/en-US/workspace/learning/courses/course-1/listing/info",
    );
  });

  it("preloads layout data and renders nested children for analytics, listing, settings, and support groups", async () => {
    render(
      await AnalyticsLayout({
        params: params(),
        children: <div>Analytics child</div>,
      } as never),
    );
    expect(screen.getByText("Analytics child")).toBeInTheDocument();
    expect(mocks.getCourseRevenueAnalytics).toHaveBeenCalledWith("course-1");

    render(
      await ListingLayout({
        params: params(),
        children: <div>Listing child</div>,
      } as never),
    );
    expect(screen.getByText("Listing child")).toBeInTheDocument();

    render(
      await AssessmentsLayout({
        params: params(),
        children: <div>Assessment child</div>,
      } as never),
    );
    expect(screen.getByText("Assessment child")).toBeInTheDocument();
    expect(mocks.getCourseAssessments).toHaveBeenCalledWith("course-1");

    render(
      await CertificatesLayout({
        params: params(),
        children: <div>Certificate child</div>,
      } as never),
    );
    expect(screen.getByText("Certificate child")).toBeInTheDocument();
    expect(mocks.getCourseCertificates).toHaveBeenCalledWith("course-1");

    render(
      await SettingsLayout({
        params: params(),
        children: <div>Settings child</div>,
      } as never),
    );
    expect(screen.getByText("Settings child")).toBeInTheDocument();

    render(
      await SupportLayout({
        params: params(),
        children: <div>Support child</div>,
      } as never),
    );
    expect(screen.getByText("Support child")).toBeInTheDocument();
    expect(mocks.getCourseDiscussions).toHaveBeenCalledWith("course-1");
  });

  it("skips optional layout preloads when feature flags are disabled", async () => {
    mocks.getCourse.mockResolvedValue({
      ...course,
      features: {
        ...course.features,
        hasPricing: false,
        hasDiscussions: false,
      },
    });

    render(
      await AnalyticsLayout({
        params: params(),
        children: <div>Analytics without pricing</div>,
      } as never),
    );
    expect(screen.getByText("Analytics without pricing")).toBeInTheDocument();
    expect(mocks.getCourseRevenueAnalytics).not.toHaveBeenCalled();

    render(
      await SupportLayout({
        params: params(),
        children: <div>Support without discussions</div>,
      } as never),
    );
    expect(screen.getByText("Support without discussions")).toBeInTheDocument();
    expect(mocks.getCourseSupportTickets).toHaveBeenCalledWith("course-1");
    expect(mocks.getCourseDiscussions).not.toHaveBeenCalled();
  });

  it("uses notFound for missing or disabled course-management route groups", async () => {
    mocks.getCourse.mockResolvedValueOnce(null);
    await expect(
      ListingLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");

    mocks.getCourse.mockResolvedValueOnce(null);
    await expect(
      SettingsLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");

    mocks.getCourse.mockResolvedValueOnce(null);
    await expect(
      AnalyticsLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");

    mocks.getCourse.mockResolvedValueOnce(null);
    await expect(
      SupportLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");

    mocks.getCourse.mockResolvedValueOnce({
      ...course,
      features: { ...course.features, hasAssessments: false },
    });
    await expect(
      AssessmentsLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");

    mocks.getCourse.mockResolvedValueOnce({
      ...course,
      features: { ...course.features, hasCertificate: false },
    });
    await expect(
      CertificatesLayout({ params: params(), children: <div /> } as never),
    ).rejects.toThrow("not-found");
  });

  it("renders course analytics states from API data", async () => {
    render(await EngagementAnalyticsPage({ params: params() } as never));
    expect(screen.getByText("Engagement Analytics")).toBeInTheDocument();
    expect(screen.getByText("Daily Active Users")).toBeInTheDocument();
    expect(screen.getByText("Content Performance")).toBeInTheDocument();

    render(await CompletionAnalyticsPage({ params: params() } as never));
    expect(screen.getByText("Completion Funnel")).toBeInTheDocument();
    expect(screen.getByText("Started")).toBeInTheDocument();

    render(await RevenueAnalyticsPage({ params: params() } as never));
    expect(screen.getByText("Revenue Sources")).toBeInTheDocument();
    expect(screen.getByText("Premium")).toBeInTheDocument();
  });

  it("renders support queues, ticket details, discussions, and discussion details", async () => {
    render(await SupportTicketsPage({ params: params() } as never));
    expect(screen.getByText("Support Queue")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /cannot submit quiz/i }),
    ).toHaveAttribute(
      "href",
      "/workspace/learning/courses/course-1/support/tickets/ticket-1",
    );

    render(
      await SupportTicketDetailPage({
        params: params({ ticketId: "ticket-1" }),
      } as never),
    );
    expect(screen.getAllByText("Cannot submit quiz").length).toBeGreaterThan(0);
    expect(screen.getByTestId("course-ticket-action-panel")).toHaveTextContent(
      "ticket-1:false",
    );

    render(await DiscussionsPage({ params: params() } as never));
    expect(screen.getByTestId("course-discussions-manager")).toHaveTextContent(
      "Advanced AI:1",
    );

    render(
      await DiscussionThreadPage({
        params: params({ threadId: "thread-1" }),
      } as never),
    );
    expect(screen.getAllByText("Week 1 help").length).toBeGreaterThan(0);
    expect(screen.getByText("Accepted answer")).toBeInTheDocument();
  });

  it("renders settings and listing management pages from API contracts", async () => {
    render(await NotificationSettingsPage({ params: params() } as never));
    expect(screen.getByText("Notification settings")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Welcome")).toBeInTheDocument();

    render(await IntegrationSettingsPage({ params: params() } as never));
    expect(screen.getByText("Course integrations")).toBeInTheDocument();
    expect(screen.getByText("Discord")).toBeInTheDocument();

    render(await ListingFaqPage({ params: params() } as never));
    expect(screen.getByTestId("faq-editor")).toHaveTextContent("1 faq items");

    render(await ListingPricingPage({ params: params() } as never));
    expect(screen.getByTestId("pricing-editor")).toHaveTextContent(
      "Refunds available",
    );

    render(await ListingProjectsPage({ params: params() } as never));
    expect(screen.getByTestId("project-carousel-editor")).toHaveTextContent(
      "1 project slides",
    );

    render(await ListingTestimonialsPage({ params: params() } as never));
    expect(screen.getByText("Testimonials & reviews")).toBeInTheDocument();
    expect(screen.getByText("Useful")).toBeInTheDocument();
  });

  it("renders the lesson authoring workspace and certificate details, and uses notFound for missing records", async () => {
    render(
      await ContentItemPage({
        params: params({ contentId: "content-1" }),
      } as never),
    );
    expect(screen.getByTestId("lesson-authoring-workspace")).toHaveTextContent(
      "Advanced AI:Lesson 1:2",
    );

    render(
      await AssessmentDetailPage({
        params: params({ assessmentId: "assessment-1" }),
      } as never),
    );
    expect(screen.getByTestId("assessment-editor")).toHaveTextContent(
      "Quiz 1:1",
    );

    render(
      await CertificateTemplateDetailPage({
        params: params({ templateId: "template-1" }),
      } as never),
    );
    expect(
      screen.getByDisplayValue("Completion certificate"),
    ).toBeInTheDocument();
    expect(
      screen.getByTitle("Certificate preview").getAttribute("srcdoc"),
    ).toContain("Certificate body");

    mocks.getContentItem.mockResolvedValueOnce(null);
    await expect(
      ContentItemPage({ params: params({ contentId: "missing" }) } as never),
    ).rejects.toThrow("not-found");

    mocks.getAssessment.mockResolvedValueOnce(null);
    await expect(
      AssessmentDetailPage({
        params: params({ assessmentId: "missing" }),
      } as never),
    ).rejects.toThrow("not-found");

    mocks.getCertificateTemplate.mockResolvedValueOnce(null);
    await expect(
      CertificateTemplateDetailPage({
        params: params({ templateId: "missing" }),
      } as never),
    ).rejects.toThrow("not-found");
  });
});
