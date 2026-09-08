import "@testing-library/jest-dom/vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ContentTree } from "./content-tree";
import type { ContentItem } from "@/lib/learning/types";
import { TooltipProvider } from "@game-guild/ui/components/tooltip";
import {
  addContent,
  deleteContent,
  moveContent,
  reorderContent,
  updateContent,
} from "@/lib/learning/actions";

const navigationMocks = vi.hoisted(() => ({
  refresh: vi.fn(),
  push: vi.fn(),
}));

Object.defineProperties(HTMLElement.prototype, {
  hasPointerCapture: { value: vi.fn(() => false) },
  setPointerCapture: { value: vi.fn() },
  releasePointerCapture: { value: vi.fn() },
  scrollIntoView: { value: vi.fn() },
});

global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
};

vi.mock("next/navigation", () => ({
  usePathname: () => "/en-US/workspace/learning/courses/course-1/content",
  useRouter: () => ({
    push: navigationMocks.push,
    refresh: navigationMocks.refresh,
  }),
}));

vi.mock("@/lib/learning/actions", () => ({
  addContent: vi.fn(),
  deleteContent: vi.fn(),
  moveContent: vi.fn(),
  reorderContent: vi.fn(),
  updateContent: vi.fn(),
}));

const moduleItem = {
  id: "module-1",
  parentId: null,
  order: 0,
  type: "Lesson",
  title: "Week 01",
  slug: "week-01",
  description: null,
  status: "published",
  visibility: "Public",
  duration: null,
  metadata: {},
  gradingConfig: null,
  createdAt: "2026-01-01T00:00:00.000Z",
  updatedAt: "2026-01-01T00:00:00.000Z",
} satisfies ContentItem;

const secondModuleItem = {
  ...moduleItem,
  id: "module-2",
  order: 1,
  title: "Week 02",
  description: "Combat systems",
  status: "draft",
  visibility: "Private",
} satisfies ContentItem;

const lessonItem = {
  id: "lesson-1",
  parentId: "module-1",
  order: 0,
  type: "Lesson",
  title: "Course overview",
  slug: "course-overview",
  description: null,
  status: "published",
  visibility: "Public",
  duration: 20,
  metadata: {},
  gradingConfig: null,
  createdAt: "2026-01-01T00:00:00.000Z",
  updatedAt: "2026-01-01T00:00:00.000Z",
} satisfies ContentItem;

const subLessonItem = {
  ...lessonItem,
  id: "sublesson-1",
  parentId: "lesson-1",
  order: 0,
  type: "Code",
  title: "Starter exercise",
  slug: "starter-exercise",
  status: "archived",
  visibility: "Private",
  duration: 0,
} satisfies ContentItem;

const secondLessonItem = {
  ...lessonItem,
  id: "lesson-2",
  title: "Setup environment",
  slug: "setup-environment",
  order: 1,
} satisfies ContentItem;

function renderContentTree({
  modules = [moduleItem],
  allItems = [moduleItem],
  virtualModuleIds = [],
}: {
  modules?: ContentItem[];
  allItems?: ContentItem[];
  virtualModuleIds?: string[];
} = {}) {
  return render(
    <TooltipProvider>
      <ContentTree
        courseId="course-1"
        modules={modules}
        allItems={allItems}
        virtualModuleIds={virtualModuleIds}
      />
    </TooltipProvider>,
  );
}

describe("ContentTree course management", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(addContent).mockResolvedValue({
      success: true,
      data: { id: "created-content" },
    });
    vi.mocked(deleteContent).mockResolvedValue({ success: true, data: null });
    vi.mocked(moveContent).mockResolvedValue({ success: true, data: null });
    vi.mocked(reorderContent).mockResolvedValue({ success: true, data: null });
    vi.mocked(updateContent).mockResolvedValue({ success: true, data: null });
  });

  it("offers the current content item types without legacy aliases", async () => {
    const user = userEvent.setup();

    renderContentTree();

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    await user.click(screen.getByRole("combobox", { name: /type/i }));

    expect(
      await screen.findByRole("option", { name: "Lesson" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Quiz" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Project" })).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Discussion" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Code" })).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Reflection" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Survey" })).toBeInTheDocument();
    expect(
      screen.queryByRole("option", { name: "Page" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("option", { name: "Challenge" }),
    ).not.toBeInTheDocument();
  });

  it("creates a top-level module with title and description", async () => {
    renderContentTree();

    fireEvent.click(screen.getByRole("button", { name: /^add module$/i }));
    const dialog = screen.getByRole("dialog", { name: /add module/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Week 02" },
    });
    fireEvent.change(within(dialog).getByLabelText(/description/i), {
      target: { value: "Combat systems" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add module$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        title: "Week 02",
        slug: "week-02",
        description: "Combat systems",
        type: "Module",
        sortOrder: 1,
      });
    });
    expect(navigationMocks.refresh).toHaveBeenCalled();
  });

  it("opens modules introduced by a server refresh so their lesson actions are immediately available", () => {
    const view = renderContentTree({
      modules: [moduleItem],
      allItems: [moduleItem],
      virtualModuleIds: ["module-1"],
    });
    const createdModule = {
      ...moduleItem,
      id: "module-created",
      type: "Module" as const,
      title: "Production Foundations",
    };

    view.rerender(
      <TooltipProvider>
        <ContentTree
          courseId="course-1"
          modules={[createdModule]}
          allItems={[createdModule]}
          virtualModuleIds={[]}
        />
      </TooltipProvider>,
    );

    expect(
      screen.getByRole("button", { name: /add lesson/i }),
    ).toBeInTheDocument();
  });

  it("creates a lesson in a module introduced by the preceding server refresh", async () => {
    const view = renderContentTree();

    fireEvent.click(screen.getByRole("button", { name: /^add module$/i }));
    let dialog = screen.getByRole("dialog", { name: /add module/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Production Foundations" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add module$/i }),
    );
    await waitFor(() =>
      expect(navigationMocks.refresh).toHaveBeenCalledTimes(1),
    );

    const createdModule = {
      ...moduleItem,
      id: "module-created",
      type: "Module" as const,
      title: "Production Foundations",
    };
    view.rerender(
      <TooltipProvider>
        <ContentTree
          courseId="course-1"
          modules={[createdModule]}
          allItems={[createdModule]}
          virtualModuleIds={[]}
        />
      </TooltipProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: /add lesson/i }));
    await screen.findByText("Production Foundations");
    dialog = await screen.findByRole("dialog", { name: /add lesson/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Define the playable promise" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenLastCalledWith({
        courseId: "course-1",
        parentId: "module-created",
        title: "Define the playable promise",
        slug: "define-the-playable-promise",
        type: "Lesson",
        lessonFormat: "Markdown",
        sortOrder: 0,
      });
    });
    expect(navigationMocks.refresh).toHaveBeenCalledTimes(2);
  });

  it("creates a lesson inside a module with the default backend type", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    await user.type(
      within(dialog).getByLabelText(/title/i),
      "Playable promise",
    );
    await user.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Playable promise",
        type: "Lesson",
        slug: "playable-promise",
        lessonFormat: "Markdown",
        sortOrder: 1,
      });
    });
    expect(navigationMocks.refresh).toHaveBeenCalled();
  }, 15_000);

  it("creates a lesson with the selected lesson format", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    await user.click(within(dialog).getByLabelText(/lesson format/i));
    await user.click(await screen.findByRole("option", { name: /video \(link\)/i }));
    await user.type(
      within(dialog).getByLabelText(/title/i),
      "Camera blocking walkthrough",
    );
    await user.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Camera blocking walkthrough",
        type: "Lesson",
        slug: "camera-blocking-walkthrough",
        lessonFormat: "Video",
        sortOrder: 1,
      });
    });
  }, 15_000);

  it("creates a lesson with a normalized custom slug that detaches from the title", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    await user.type(within(dialog).getByLabelText(/title/i), "Playable promise");
    expect(within(dialog).getByLabelText(/url slug/i)).toHaveValue(
      "playable-promise",
    );

    await user.clear(within(dialog).getByLabelText(/url slug/i));
    await user.type(
      within(dialog).getByLabelText(/url slug/i),
      "My Custom SLUG!",
    );
    await user.type(within(dialog).getByLabelText(/title/i), " v2");
    expect(within(dialog).getByLabelText(/url slug/i)).toHaveValue(
      "my-custom-slug",
    );

    await user.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Playable promise v2",
          slug: "my-custom-slug",
        }),
      );
    });
  }, 15_000);

  it("strips the slug trailing dash on blur in the add lesson dialog", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    const slugInput = within(dialog).getByLabelText(/url slug/i);

    await user.type(slugInput, "my slug ");
    expect(slugInput).toHaveValue("my-slug-");

    fireEvent.blur(slugInput);
    expect(slugInput).toHaveValue("my-slug");
  }, 15_000);

  it("hides the lesson format selector for non-lesson content types", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    await user.click(within(dialog).getByLabelText(/type/i));
    await user.click(await screen.findByRole("option", { name: "Quiz" }));

    expect(
      within(dialog).queryByLabelText(/lesson format/i),
    ).not.toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/title/i), "Entry quiz");
    await user.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Entry quiz",
        type: "Questionnaire",
        slug: "entry-quiz",
        sortOrder: 1,
      });
    });
  }, 15_000);

  it("duplicates lesson items from the content tree", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem, secondLessonItem] });

    await user.click(
      screen.getAllByRole("button", { name: /^duplicate$/i })[0]!,
    );
    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Course overview (copy)",
        description: undefined,
        type: "Lesson",
      });
    });
  });

  it("navigates from visible lesson action icons to the content editor", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /edit lesson/i }));

    expect(navigationMocks.push).toHaveBeenCalledWith(
      "/en-US/workspace/learning/courses/course-1/content/course-overview",
    );
  });

  it("duplicates modules from always-visible action icons", async () => {
    const user = userEvent.setup();
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem],
    });

    await user.click(
      screen.getAllByRole("button", { name: /duplicate module/i })[0]!,
    );
    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: undefined,
        title: "Week 01 (copy)",
        description: undefined,
        type: "Lesson",
      });
    });
  });

  it("moves modules from always-visible action icons", async () => {
    const user = userEvent.setup();
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem],
    });

    await user.click(
      screen.getAllByRole("button", { name: /move module down/i })[0]!,
    );
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "module-2",
        "module-1",
      ]);
    });
  });

  it("moves modules upward from enabled module action icons", async () => {
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem],
    });

    fireEvent.click(
      screen.getAllByRole("button", { name: /move module up/i })[1]!,
    );
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "module-2",
        "module-1",
      ]);
    });
  });

  it("collapses and reopens modules from the chevron trigger", () => {
    const { container } = renderContentTree({
      allItems: [moduleItem, lessonItem],
    });

    expect(screen.getByText("Course overview")).toBeInTheDocument();
    const trigger = container.querySelector('button[aria-expanded="true"]');
    expect(trigger).toBeTruthy();

    fireEvent.click(trigger!);
    expect(screen.queryByText("Course overview")).not.toBeInTheDocument();

    const closedTrigger = container.querySelector(
      'button[aria-expanded="false"]',
    );
    expect(closedTrigger).toBeTruthy();
    fireEvent.click(closedTrigger!);
    expect(screen.getByText("Course overview")).toBeInTheDocument();
  });

  it("keeps add module server errors visible for retry and allows canceling", async () => {
    vi.mocked(addContent).mockResolvedValueOnce({
      success: false,
      error: "Module could not be created.",
    });
    renderContentTree();

    fireEvent.click(screen.getByRole("button", { name: /^add module$/i }));
    const dialog = screen.getByRole("dialog", { name: /add module/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Week 03" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add module$/i }),
    );

    expect(
      await screen.findByText("Module could not be created."),
    ).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: /cancel/i }));
    expect(
      screen.queryByRole("dialog", { name: /add module/i }),
    ).not.toBeInTheDocument();
  });

  it("keeps add lesson server errors visible and allows retrying without losing the draft", async () => {
    vi.mocked(addContent).mockResolvedValueOnce({
      success: false,
      error: "Lesson could not be created.",
    });
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    fireEvent.click(screen.getByRole("button", { name: /add lesson/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Broken lesson" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    expect(
      await screen.findByText("Lesson could not be created."),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/title/i)).toHaveValue(
      "Broken lesson",
    );
    await waitFor(() =>
      expect(
        within(dialog).getByRole("button", { name: /^add lesson$/i }),
      ).toBeEnabled(),
    );
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => expect(addContent).toHaveBeenCalledTimes(2));
    expect(
      screen.queryByRole("dialog", { name: /add lesson/i }),
    ).not.toBeInTheDocument();
    expect(navigationMocks.refresh).toHaveBeenCalled();
  });

  it("creates virtual-module content without persisting the virtual parent id", async () => {
    renderContentTree({
      allItems: [moduleItem, lessonItem],
      virtualModuleIds: ["module-1"],
    });

    expect(
      screen.queryByRole("button", { name: /edit module/i }),
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /add content item/i }));
    const dialog = screen.getByRole("dialog", { name: /add lesson/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Imported activity" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: undefined,
        title: "Imported activity",
        type: "Lesson",
        slug: "imported-activity",
        lessonFormat: "Markdown",
        sortOrder: 1,
      });
    });
  });

  it("creates submodules and nested lessons inside existing lessons", async () => {
    renderContentTree({ allItems: [moduleItem, lessonItem, subLessonItem] });

    expect(screen.getByText("1 sub-items")).toBeInTheDocument();
    expect(screen.getByText("Starter exercise")).toBeInTheDocument();
    expect(screen.getByText("Code")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /add submodule/i }));
    let dialog = screen.getByRole("dialog", { name: /add submodule/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Part A" },
    });
    fireEvent.change(within(dialog).getByLabelText(/description/i), {
      target: { value: "Nested foundations" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /add submodule/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Part A",
        description: "Nested foundations",
        slug: "part-a",
        type: "Module",
        sortOrder: 1,
      });
    });

    vi.clearAllMocks();
    fireEvent.click(
      screen.getByRole("button", { name: /add to course overview/i }),
    );
    dialog = screen.getByRole("dialog", { name: /add lesson/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Nested reflection" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^add lesson$/i }),
    );

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "lesson-1",
        title: "Nested reflection",
        type: "Lesson",
        slug: "nested-reflection",
        lessonFormat: "Markdown",
        sortOrder: 1,
      });
    });
  });

  it("keeps submodule server errors visible for retry and supports canceling", async () => {
    vi.mocked(addContent).mockResolvedValueOnce({
      success: false,
      error: "Submodule could not be created.",
    });
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    fireEvent.click(screen.getByRole("button", { name: /add submodule/i }));
    const dialog = screen.getByRole("dialog", { name: /add submodule/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Part B" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /add submodule/i }),
    );

    expect(
      await screen.findByText("Submodule could not be created."),
    ).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: /cancel/i }));
    expect(
      screen.queryByRole("dialog", { name: /add submodule/i }),
    ).not.toBeInTheDocument();
  });

  it("reorders lesson items from the content tree", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem, secondLessonItem] });

    await user.click(
      screen.getAllByRole("button", { name: /^move down$/i })[0]!,
    );
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "lesson-2",
        "lesson-1",
      ]);
    });
  });

  it("moves lessons upward from enabled lesson action icons", async () => {
    renderContentTree({ allItems: [moduleItem, lessonItem, secondLessonItem] });

    fireEvent.click(screen.getAllByRole("button", { name: /^move up$/i })[1]!);
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "lesson-2",
        "lesson-1",
      ]);
    });
  });

  it("handles module move server errors without refreshing", async () => {
    vi.mocked(reorderContent).mockResolvedValueOnce({
      success: false,
      error: "Module order failed.",
    });
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem, lessonItem, secondLessonItem],
    });

    fireEvent.click(
      screen.getAllByRole("button", { name: /move module down/i })[0]!,
    );
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "module-2",
        "module-1",
      ]);
    });
    expect(navigationMocks.refresh).not.toHaveBeenCalled();
  });

  it("handles lesson move server errors without refreshing", async () => {
    vi.mocked(reorderContent).mockResolvedValueOnce({
      success: false,
      error: "Lesson order failed.",
    });
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem, lessonItem, secondLessonItem],
    });

    fireEvent.click(
      screen.getAllByRole("button", { name: /^move down$/i })[0]!,
    );
    await waitFor(() => {
      expect(reorderContent).toHaveBeenCalledWith("course-1", [
        "lesson-2",
        "lesson-1",
      ]);
    });
    expect(navigationMocks.refresh).not.toHaveBeenCalled();
  });

  it("ignores unavailable move buttons at the module and lesson boundaries", () => {
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem, lessonItem, secondLessonItem],
    });

    fireEvent.click(
      screen.getAllByRole("button", { name: /move module up/i })[0]!,
    );
    fireEvent.click(
      screen.getAllByRole("button", { name: /move module down/i })[1]!,
    );
    fireEvent.click(screen.getAllByRole("button", { name: /^move up$/i })[0]!);
    fireEvent.click(
      screen.getAllByRole("button", { name: /^move down$/i })[1]!,
    );

    expect(reorderContent).not.toHaveBeenCalled();
  });

  it("deletes lesson items from the content tree", async () => {
    const user = userEvent.setup();
    renderContentTree({ allItems: [moduleItem, lessonItem, secondLessonItem] });

    await user.click(screen.getAllByRole("button", { name: /^delete$/i })[0]!);
    const deleteDialog = screen.getByRole("dialog", { name: /delete item/i });
    expect(deleteDialog).toHaveTextContent("Course overview");
    await user.click(
      within(deleteDialog).getByRole("button", { name: /^delete$/i }),
    );

    await waitFor(() => {
      expect(deleteContent).toHaveBeenCalledWith("course-1", "lesson-1");
    });
  });

  it("keeps deletion errors visible for retry", async () => {
    const user = userEvent.setup();
    vi.mocked(deleteContent).mockResolvedValueOnce({
      success: false,
      error: "Content item is locked.",
    });
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    await user.click(screen.getByRole("button", { name: /^delete$/i }));
    const deleteDialog = screen.getByRole("dialog", { name: /delete item/i });
    await user.click(
      within(deleteDialog).getByRole("button", { name: /^delete$/i }),
    );

    expect(
      await screen.findByText("Content item is locked."),
    ).toBeInTheDocument();
  });

  it("deletes modules with the module-specific confirmation copy and supports canceling deletion", async () => {
    const user = userEvent.setup();
    renderContentTree();

    await user.click(screen.getByRole("button", { name: /delete module/i }));
    const deleteDialog = screen.getByRole("dialog", { name: /delete module/i });
    expect(deleteDialog).toHaveTextContent(
      "All lessons within this module will also be deleted.",
    );
    fireEvent.click(
      within(deleteDialog).getByRole("button", { name: /cancel/i }),
    );
    expect(
      screen.queryByRole("dialog", { name: /delete module/i }),
    ).not.toBeInTheDocument();
  });

  it("edits modules and surfaces server-action validation failures", async () => {
    vi.mocked(updateContent).mockResolvedValueOnce({
      success: false,
      error: "Module title already exists.",
    });
    renderContentTree();

    fireEvent.click(screen.getByRole("button", { name: /edit module/i }));
    const dialog = screen.getByRole("dialog", { name: /edit module/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Week 01 revised" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /save changes/i }),
    );

    await waitFor(() => {
      expect(updateContent).toHaveBeenCalledWith({
        courseId: "course-1",
        contentId: "module-1",
        title: "Week 01 revised",
        slug: "week-01-revised",
        description: "",
      });
    });
    expect(
      await screen.findByText("Module title already exists."),
    ).toBeInTheDocument();
  });

  it("saves module edits successfully and supports canceling edits", async () => {
    renderContentTree();

    fireEvent.click(
      screen.getAllByRole("button", { name: /edit module/i })[0]!,
    );
    let dialog = screen.getByRole("dialog", { name: /edit module/i });
    fireEvent.change(within(dialog).getByLabelText(/title/i), {
      target: { value: "Week 01 updated" },
    });
    fireEvent.change(within(dialog).getByLabelText(/description/i), {
      target: { value: "Updated module scope" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: /save changes/i }),
    );

    await waitFor(() => {
      expect(updateContent).toHaveBeenCalledWith({
        courseId: "course-1",
        contentId: "module-1",
        title: "Week 01 updated",
        slug: "week-01-updated",
        description: "Updated module scope",
      });
    });
    await waitFor(() => {
      expect(navigationMocks.refresh).toHaveBeenCalled();
    });

    fireEvent.click(
      screen.getAllByRole("button", { name: /edit module/i })[0]!,
    );
    dialog = screen.getByRole("dialog", { name: /edit module/i });
    fireEvent.click(within(dialog).getByRole("button", { name: /cancel/i }));
    expect(
      screen.queryByRole("dialog", { name: /edit module/i }),
    ).not.toBeInTheDocument();
  });

  it("keeps duplicate errors from refreshing the content tree", async () => {
    vi.mocked(addContent).mockResolvedValueOnce({
      success: false,
      error: "Duplicate failed.",
    });
    renderContentTree({ allItems: [moduleItem, lessonItem] });

    fireEvent.click(screen.getByRole("button", { name: /^duplicate$/i }));

    await waitFor(() => {
      expect(addContent).toHaveBeenCalledWith({
        courseId: "course-1",
        parentId: "module-1",
        title: "Course overview (copy)",
        description: undefined,
        type: "Lesson",
      });
    });
    expect(navigationMocks.refresh).not.toHaveBeenCalled();
  });

  it("closes management dialogs through the dialog close affordance", () => {
    renderContentTree({
      modules: [moduleItem, secondModuleItem],
      allItems: [moduleItem, secondModuleItem, lessonItem],
    });

    fireEvent.click(screen.getByRole("button", { name: /^delete$/i }));
    fireEvent.click(screen.getByRole("button", { name: /^close$/i }));
    expect(
      screen.queryByRole("dialog", { name: /delete item/i }),
    ).not.toBeInTheDocument();

    fireEvent.click(
      screen.getAllByRole("button", { name: /edit module/i })[0]!,
    );
    fireEvent.click(screen.getByRole("button", { name: /^close$/i }));
    expect(
      screen.queryByRole("dialog", { name: /edit module/i }),
    ).not.toBeInTheDocument();

    fireEvent.click(
      screen.getAllByRole("button", { name: /add submodule/i })[0]!,
    );
    fireEvent.click(screen.getByRole("button", { name: /^close$/i }));
    expect(
      screen.queryByRole("dialog", { name: /add submodule/i }),
    ).not.toBeInTheDocument();
  });

  it("uses subitem edit and delete actions from nested content rows", async () => {
    renderContentTree({ allItems: [moduleItem, lessonItem, subLessonItem] });

    fireEvent.click(screen.getByRole("button", { name: /^edit$/i }));
    expect(navigationMocks.push).toHaveBeenCalledWith(
      "/en-US/workspace/learning/courses/course-1/content/starter-exercise",
    );

    fireEvent.click(screen.getAllByRole("button", { name: /^delete$/i })[1]!);
    const dialog = screen.getByRole("dialog", { name: /delete item/i });
    expect(dialog).toHaveTextContent("Starter exercise");
    fireEvent.click(within(dialog).getByRole("button", { name: /^delete$/i }));

    await waitFor(() => {
      expect(deleteContent).toHaveBeenCalledWith("course-1", "sublesson-1");
    });
  });
});
