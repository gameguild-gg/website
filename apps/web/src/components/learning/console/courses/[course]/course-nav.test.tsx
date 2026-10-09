import "@testing-library/jest-dom/vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ComponentProps, ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CourseNav as CourseNavComponent } from "./course-nav";
import type { CourseAccessCapabilities } from "@/lib/learning/queries/course";

const refreshMock = vi.fn();
const actionMocks = vi.hoisted(() => ({
  publishCourse: vi.fn(),
  restoreCourse: vi.fn(),
  unpublishCourse: vi.fn(),
}));
const navigationMocks = vi.hoisted(() => ({
  pathname: vi.fn(),
}));

vi.mock("@/i18n/navigation", () => ({
  Link: ({
    children,
    href,
    locale,
    prefetch: _prefetch,
    ...rest
  }: {
    children: ReactNode;
    href: string;
    locale?: string;
    prefetch?: boolean;
  }) => (
    <a href={href} data-locale={locale} {...rest}>
      {children}
    </a>
  ),
  usePathname: navigationMocks.pathname,
  useRouter: () => ({ refresh: refreshMock }),
}));

vi.mock("@/lib/learning/actions", () => ({
  publishCourse: actionMocks.publishCourse,
  restoreCourse: actionMocks.restoreCourse,
  unpublishCourse: actionMocks.unpublishCourse,
}));

const enabledFeatures = {
  hasClasses: true,
  hasRecordings: true,
  hasSchedule: true,
  hasOnDemandContent: true,
  hasPricing: true,
  hasCertificate: true,
  hasAssessments: true,
  hasDiscussions: true,
};

const ownerAccess: CourseAccessCapabilities = {
  courseId: "course-1",
  courseExists: true,
  isTenantMember: true,
  isOwner: true,
  hasActiveEnrollment: false,
  canLearn: false,
  canEdit: true,
  canPublish: true,
  canReviewAsStaff: true,
  canAccessWorkspace: true,
};

function CourseNav({
  access = ownerAccess,
  ...props
}: Omit<ComponentProps<typeof CourseNavComponent>, "access"> & {
  access?: CourseAccessCapabilities;
}) {
  return <CourseNavComponent {...props} access={access} />;
}

describe("CourseNav", () => {
  const writeTextMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    actionMocks.publishCourse.mockResolvedValue({ success: true, data: null });
    actionMocks.restoreCourse.mockResolvedValue({ success: true, data: null });
    actionMocks.unpublishCourse.mockResolvedValue({
      success: true,
      data: null,
    });
    navigationMocks.pathname.mockReturnValue(
      "/en-US/workspace/learning/courses/ai-for-boss-encounters/listing",
    );
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: writeTextMock },
    });
  });

  it("links the preview action to the authenticated dashboard storefront preview", () => {
    render(
      <CourseNav
        courseTitle="AI for Boss Encounters"
        courseDescription="Build readable encounter AI."
        courseStatus="published"
        courseSlug="ai-for-boss-encounters"
        courseRouteParam="ai-for-boss-encounters"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Course editor content</div>
      </CourseNav>,
    );

    const previewLink = screen.getByRole("link", { name: /preview/i });
    expect(previewLink).toHaveAttribute(
      "href",
      "/workspace/learning/courses/ai-for-boss-encounters/preview",
    );
    expect(previewLink).toHaveAttribute("data-locale", "en-US");
    expect(screen.getAllByText("Course editor content")).toHaveLength(1);
  });

  it("copies the public storefront course URL from the share action", async () => {
    render(
      <CourseNav
        courseTitle="AI for Boss Encounters"
        courseDescription="Build readable encounter AI."
        courseStatus="published"
        courseSlug="ai-for-boss-encounters"
        courseRouteParam="ai-for-boss-encounters"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Course editor content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /share/i }));

    expect(writeTextMock).toHaveBeenCalledWith(
      "http://localhost:3000/courses/ai-for-boss-encounters",
    );
    expect(
      await screen.findByRole("button", { name: /copied/i }),
    ).toBeInTheDocument();
  });

  it("keeps public sharing disabled when a course has no public slug", () => {
    render(
      <CourseNav
        courseTitle="Untitled Draft"
        courseDescription="Draft course."
        courseStatus="draft"
        courseSlug=""
        courseRouteParam="untitled-draft"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Course editor content</div>
      </CourseNav>,
    );

    expect(screen.getByRole("link", { name: /preview/i })).toHaveAttribute(
      "href",
      "/workspace/learning/courses/untitled-draft/preview",
    );
    expect(screen.getByRole("button", { name: /share/i })).toBeDisabled();
  });

  it("confirms before unpublishing a published course", async () => {
    render(
      <CourseNav
        courseTitle="AI for Boss Encounters"
        courseDescription="Build readable encounter AI."
        courseStatus="published"
        courseSlug="ai-for-boss-encounters"
        courseRouteParam="ai-for-boss-encounters-by-gameguild"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Course editor content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /^unpublish$/i }));
    expect(
      screen.getByRole("heading", { name: /unpublish this course/i }),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /unpublish course/i }));

    expect(actionMocks.unpublishCourse).toHaveBeenCalledWith(
      "ai-for-boss-encounters-by-gameguild",
    );
    expect(
      await screen.findByRole("button", { name: /^publish$/i }),
    ).toBeEnabled();
  });

  it("restores an archived course to draft before it can be published again", async () => {
    render(
      <CourseNav
        courseTitle="AI for Boss Encounters"
        courseDescription="Build readable encounter AI."
        courseStatus="archived"
        courseSlug="ai-for-boss-encounters"
        courseRouteParam="ai-for-boss-encounters-by-gameguild"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Course editor content</div>
      </CourseNav>,
    );

    const restoreButton = screen.getByRole("button", { name: /^restore$/i });
    expect(restoreButton).toBeEnabled();
    fireEvent.click(restoreButton);

    expect(actionMocks.restoreCourse).toHaveBeenCalledWith(
      "ai-for-boss-encounters-by-gameguild",
    );
    expect(actionMocks.publishCourse).not.toHaveBeenCalled();
    expect(await screen.findByText("Draft")).toBeInTheDocument();
    expect(
      await screen.findByRole("button", { name: /^publish$/i }),
    ).toBeEnabled();
  });

  it("publishes a draft and refreshes the server route", async () => {
    render(
      <CourseNav
        courseTitle="Draft course"
        courseDescription="Ready to publish."
        courseStatus="draft"
        courseSlug="draft-course"
        courseRouteParam="draft-course-by-ada"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Draft content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    expect(actionMocks.publishCourse).toHaveBeenCalledWith(
      "draft-course-by-ada",
    );
    expect(await screen.findByText("Published")).toBeInTheDocument();
    expect(refreshMock).toHaveBeenCalled();
  });

  it("shows lifecycle API failures without changing the current status", async () => {
    actionMocks.publishCourse.mockResolvedValueOnce({
      success: false,
      error: "Publishing is not allowed.",
    });
    render(
      <CourseNav
        courseTitle="Draft course"
        courseDescription="Not ready."
        courseStatus="draft"
        courseSlug="draft-course"
        courseRouteParam="draft-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Draft content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    expect(
      await screen.findByText("Publishing is not allowed."),
    ).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
  });

  it("normalizes thrown Error and unknown lifecycle failures", async () => {
    actionMocks.publishCourse
      .mockRejectedValueOnce(new Error("Publishing service is offline."))
      .mockRejectedValueOnce("offline");
    const view = render(
      <CourseNav
        courseTitle="Draft course"
        courseDescription="Retry publishing."
        courseStatus="draft"
        courseSlug="draft-course"
        courseRouteParam="draft-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Draft content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    expect(
      await screen.findByText("Publishing service is offline."),
    ).toBeInTheDocument();

    view.rerender(
      <CourseNav
        courseTitle="Draft course"
        courseDescription="Retry publishing."
        courseStatus="draft"
        courseSlug="draft-course"
        courseRouteParam="draft-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Draft content</div>
      </CourseNav>,
    );
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    expect(
      await screen.findByText("The course lifecycle action failed."),
    ).toBeInTheDocument();
  });

  it("disables lifecycle controls while an action is pending", async () => {
    let resolvePublish!: (value: { success: true; data: null }) => void;
    actionMocks.publishCourse.mockReturnValueOnce(
      new Promise((resolve) => {
        resolvePublish = resolve;
      }),
    );
    render(
      <CourseNav
        courseTitle="Draft course"
        courseDescription="Publishing."
        courseStatus="draft"
        courseSlug="draft-course"
        courseRouteParam="draft-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Draft content</div>
      </CourseNav>,
    );

    const publish = screen.getByRole("button", { name: /^publish$/i });
    fireEvent.click(publish);
    await waitFor(() => expect(publish).toBeDisabled());
    resolvePublish({ success: true, data: null });
    await screen.findByText("Published");
  });

  it("cancels unpublishing without invoking the lifecycle action", async () => {
    render(
      <CourseNav
        courseTitle="Published course"
        courseDescription="Public."
        courseStatus="published"
        courseSlug="published-course"
        courseRouteParam="published-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Published content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /^unpublish$/i }));
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() =>
      expect(
        screen.queryByText("Unpublish this course?"),
      ).not.toBeInTheDocument(),
    );
    expect(actionMocks.unpublishCourse).not.toHaveBeenCalled();
  });

  it("omits disabled feature navigation and handles paths outside the course workspace", () => {
    navigationMocks.pathname.mockReturnValue("/workspace/elsewhere");
    const features = {
      ...enabledFeatures,
      hasClasses: false,
      hasAssessments: false,
      hasCertificate: false,
    };
    const view = render(
      <CourseNav
        courseTitle="Focused course"
        courseDescription="Minimal navigation."
        courseStatus="draft"
        courseSlug="focused-course"
        courseRouteParam="focused-course"
        locale="en-US"
        features={features}
      >
        <div>Focused content</div>
      </CourseNav>,
    );

    expect(screen.queryByText("Classes")).not.toBeInTheDocument();
    expect(screen.queryByText("Assessments")).not.toBeInTheDocument();
    expect(screen.queryByText("Certificates")).not.toBeInTheDocument();

    navigationMocks.pathname.mockReturnValue(
      "/workspace/learning/courses/focused-course",
    );
    view.rerender(
      <CourseNav
        courseTitle="Focused course"
        courseDescription="Minimal navigation."
        courseStatus="draft"
        courseSlug="focused-course"
        courseRouteParam="focused-course"
        locale="en-US"
        features={features}
      >
        <div>Focused content</div>
      </CourseNav>,
    );
    expect(screen.getAllByText("Overview")).toHaveLength(2);

    navigationMocks.pathname.mockReturnValue(null);
    view.rerender(
      <CourseNav
        courseTitle="Focused course"
        courseDescription="Minimal navigation."
        courseStatus="draft"
        courseSlug="focused-course"
        courseRouteParam="focused-course"
        locale="en-US"
        features={features}
      >
        <div>Focused content</div>
      </CourseNav>,
    );
    expect(screen.getAllByText("Overview")).toHaveLength(2);
  });

  it("does not copy when the clipboard API is unavailable", () => {
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: undefined,
    });
    render(
      <CourseNav
        courseTitle="Public course"
        courseDescription="Shareable."
        courseStatus="draft"
        courseSlug="public-course"
        courseRouteParam="public-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Public content</div>
      </CourseNav>,
    );

    fireEvent.click(screen.getByRole("button", { name: /share/i }));
    expect(screen.getByRole("button", { name: /share/i })).toBeInTheDocument();
  });

  it("syncs changed and unknown course statuses from refreshed props", async () => {
    const view = render(
      <CourseNav
        courseTitle="Changing course"
        courseDescription="Status changes."
        courseStatus={"unknown" as never}
        courseSlug={null}
        courseRouteParam="changing-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Changing content</div>
      </CourseNav>,
    );
    expect(screen.queryByText("Draft")).not.toBeInTheDocument();

    view.rerender(
      <CourseNav
        courseTitle="Changing course"
        courseDescription="Status changes."
        courseStatus="published"
        courseSlug={null}
        courseRouteParam="changing-course"
        locale="en-US"
        features={enabledFeatures}
      >
        <div>Changing content</div>
      </CourseNav>,
    );
    expect(await screen.findByText("Published")).toBeInTheDocument();
  });

  it("gives a review-only collaborator grading navigation without authoring controls", () => {
    render(
      <CourseNav
        courseTitle="Review course"
        courseDescription="Review only."
        courseStatus="published"
        courseSlug="review-course"
        courseRouteParam="review-course"
        locale="en-US"
        features={enabledFeatures}
        access={{
          ...ownerAccess,
          isOwner: false,
          canEdit: false,
          canPublish: false,
          canReviewAsStaff: true,
        }}
      >
        <div>Review workspace</div>
      </CourseNav>,
    );

    expect(screen.getAllByText("Assessments")).not.toHaveLength(0);
    expect(screen.getAllByText("Students")).not.toHaveLength(0);
    expect(screen.queryByText("Content")).not.toBeInTheDocument();
    expect(screen.queryByText("Settings")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /publish/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /preview/i })).not.toBeInTheDocument();
  });
});
