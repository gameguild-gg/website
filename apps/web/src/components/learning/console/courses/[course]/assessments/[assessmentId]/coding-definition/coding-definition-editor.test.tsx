import "@testing-library/jest-dom/vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const routerMocks = vi.hoisted(() => ({
  push: vi.fn(),
}));

const ideMock = vi.hoisted(() => {
  const getFiles = vi.fn<
    () => Promise<Array<{ path: string; content: string; type: "text" }>>
  >();

  return {
    getFiles,
    replaceFiles: vi.fn(
      async (files: Array<{ path: string; content: string; type: "text" }>) => {
        getFiles.mockResolvedValue(files);
      },
    ),
  };
});

const putMock = vi.hoisted(() => vi.fn());

const assignmentSamplesMock = vi.hoisted(() => ({
  cpp: {
    workspaceConfig: {
      id: "cpp",
      label: "C++ Assignment",
      version: 1,
      files: {
        "/user/main.cpp": {
          encoding: "text",
          content: "// C++ assignment starter",
        },
      },
    },
    plan: {
      cases: [
        {
          kind: "stdio",
          name: "echo stdin",
          stdin: "hello world",
          expectedStdout: "hello world",
          weight: 2,
          hidden: false,
        },
      ],
      build: { sources: ["/user/main.cpp"] },
    },
  },
  "allegro-cpp": {
    workspaceConfig: {
      id: "allegro-cpp",
      label: "Allegro 5 C++ Assignment",
      version: 1,
      files: {
        "/user/allegro-main.cpp": {
          encoding: "text",
          content: "// Allegro 5 assignment starter",
        },
      },
    },
    plan: {
      cases: [{ kind: "custom", name: "renders without crashing", weight: 1, hidden: false }],
      build: { sources: ["/user/allegro-main.cpp"] },
    },
  },
}));

// Capture the latest composed editor props without booting the WASM runtime.
let lastIdeProps: {
  workspaceConfig?: { id?: string; files?: Record<string, unknown> };
  allowCreateFiles?: boolean;
} = {};
let lastAssessmentEditorProps: {
  mode?: string;
  workspaceConfig?: { id?: string; files?: Record<string, unknown> };
  extensions?: readonly { id: string }[];
} | null = null;

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
  usePathname: () => '/workspace/learning',
  useRouter: () => routerMocks,
}));

vi.mock("@game-guild/emception-ui", async () => {
  const React = await import("react");

  function CodingAssessmentEditor(props: {
    mode?: string;
    definition?: {
      Environment?: { AllowStudentCreateFiles?: boolean };
      Tests?: { Public?: unknown[]; Private?: unknown[] };
    };
    workspaceConfig?: { id?: string; files?: Record<string, unknown> };
    extensions?: readonly {
      id: string;
      toolbarEnd?: () => React.ReactNode;
      explorerFooter?: (controller: {
        getFiles: typeof ideMock.getFiles;
        replaceFiles: typeof ideMock.replaceFiles;
      }) => React.ReactNode;
      bottomPanel?: () => React.ReactNode;
    }[];
    onReady?: (controller: {
      getFiles: typeof ideMock.getFiles;
      replaceFiles: typeof ideMock.replaceFiles;
    }) => void;
  }) {
    lastAssessmentEditorProps = props;
    lastIdeProps = {
      workspaceConfig: props.workspaceConfig,
      allowCreateFiles: props.definition?.Environment?.AllowStudentCreateFiles,
    };
    const { onReady } = props;
    React.useEffect(() => {
      onReady?.({
        getFiles: ideMock.getFiles,
        replaceFiles: ideMock.replaceFiles,
      });
    }, [onReady]);
    return React.createElement(
      "div",
      { "data-testid": "mock-assessment-editor" },
      React.createElement(
        "div",
        { "data-testid": "mock-assessment-surface" },
        ...((props.extensions ?? []).map((extension) =>
          React.createElement(
            React.Fragment,
            { key: extension.id },
            extension.toolbarEnd?.(),
            extension.explorerFooter?.({
              getFiles: ideMock.getFiles,
              replaceFiles: ideMock.replaceFiles,
            }),
            extension.bottomPanel?.(),
          ),
        )),
      ),
    );
  }

  return {
    CodingAssessmentEditor,
  };
});

vi.mock("@game-guild/emception-ui/assessment/presets", () => ({
  ASSIGNMENT_SAMPLES: assignmentSamplesMock,
  createAssessmentWorkspaceConfig: (
    language: keyof typeof assignmentSamplesMock,
    files: Record<string, unknown>,
  ) => ({
    ...assignmentSamplesMock[language].workspaceConfig,
    files,
  }),
}));

vi.mock("@/lib/coding-assignment/actions", async () => {
  const actual = await vi.importActual<
    typeof import("@/lib/coding-assignment/actions")
  >("@/lib/coding-assignment/actions");
  return {
    ...actual,
    putCodingAssignmentAction: putMock,
  };
});

import { CodingDefinitionEditor } from "./coding-definition-editor";
import { ASSIGNMENT_SAMPLES } from "@game-guild/emception-ui/assessment/presets";
import { putCodingAssignmentAction } from "@/lib/coding-assignment/actions";
import type { CodingAssignmentContent } from "@/lib/coding-assignment/client";

const baseProps = {
  courseId: "course-1",
  assessmentId: "assessment-1",
  programId: "course-1",
  contentId: "content-1",
  assessmentTitle: "Echo Assignment",
  initialContent: null,
};

describe("CodingDefinitionEditor", () => {
  beforeEach(() => {
    lastAssessmentEditorProps = null;
    vi.clearAllMocks();
    lastIdeProps = {};
    ideMock.getFiles.mockReset();
    ideMock.getFiles.mockResolvedValue([
      { path: "/user/main.cpp", content: "// edited starter", type: "text" },
    ]);
    putMock.mockReset();
    putMock.mockResolvedValue({ success: true });
  });

  it("composes the author workspace through CodingAssessmentEditor", async () => {
    render(<CodingDefinitionEditor {...baseProps} initialContent={null} />);

    await screen.findByTestId("mock-assessment-editor");
    expect(lastAssessmentEditorProps?.mode).toBe("author");
    expect(lastAssessmentEditorProps?.workspaceConfig?.id).toBe("cpp");
    expect(lastAssessmentEditorProps?.extensions).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ id: "gameguild-assessment-authoring" }),
      ]),
    );
  });

  it("keeps GameGuild file policies in its authoring extension", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} initialContent={null} />);

    await act(async () => {});
    ideMock.getFiles.mockResolvedValue([
      { path: "/user/main.cpp", content: "// edited starter", type: "text" },
      { path: "/user/private-helper.cpp", content: "int hidden() { return 42; }", type: "text" },
    ]);
    await user.click(await screen.findByTestId("sync-file-policies"));
    const visibility = await screen.findByLabelText(
      "Visibility for /user/private-helper.cpp",
    );
    await user.selectOptions(visibility, "Private");

    expect(visibility).toHaveValue("Private");
  });

  it("auto-seeds the C++ sample on mount when initialContent is null", async () => {
    render(<CodingDefinitionEditor {...baseProps} initialContent={null} />);

    const sample = ASSIGNMENT_SAMPLES.cpp;

    // The IDE only renders once the seed useEffect populates fileRows.
    await screen.findByTestId("mock-assessment-surface");
    expect(
      Object.keys(lastIdeProps.workspaceConfig?.files ?? {}),
    ).toContain("/user/main.cpp");
    expect(sample.workspaceConfig.id).toBeDefined();
  });

  it("adds two standard tests (one Private) and PUTs the v1 TestSuite buckets", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-standard"));
    await user.click(screen.getByTestId("add-standard"));

    // First test: stdin + Stdout, Public
    fireEvent.change(screen.getByTestId("standard-stdin-0"), {
      target: { value: "abc" },
    });
    fireEvent.change(screen.getByTestId("standard-stdout-0"), {
      target: { value: "ABC" },
    });

    // Second test: stdin + Stdout, Private
    fireEvent.change(screen.getByTestId("standard-stdin-1"), {
      target: { value: "xyz" },
    });
    fireEvent.change(screen.getByTestId("standard-stdout-1"), {
      target: { value: "XYZ" },
    });
    await user.click(screen.getByTestId("standard-visibility-1"));
    await user.click(within(await screen.findByRole("listbox")).getByRole("option", { name: "Private" }));

    await user.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    });
    const payloadArg = putCodingAssignmentAction.mock.calls[0][2] as CodingAssignmentContent;
    const payloadText = JSON.stringify(payloadArg, null, 2);

    // v1 wire format — PascalCase
    expect(payloadText).toContain('"Stdin": "abc"');
    expect(payloadText).toContain('"Stdout": "ABC"');
    expect(payloadText).toContain('"Stdin": "xyz"');
    expect(payloadText).toContain('"Stdout": "XYZ"');
    // Tests bucket split by Visibility: Public has 1, Private has 1.
    expect(payloadText).toMatch(/"Public":[\s\S]*"kind": "standard"/);
    expect(payloadText).toMatch(/"Private":[\s\S]*"kind": "standard"/);
  });

  it("offers C++ + Allegro 5 and saves Language=allegro-cpp, Tools=clang, LibBundle=allegro", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    const picker = await screen.findByTestId("preset-picker");
    expect(
      screen.getByRole("option", { name: "C++ + Allegro 5 (clang + wasm-ld)" }),
    ).toBeInTheDocument();

    // Switch to Allegro — seeds the allegro starter into the workspace.
    await user.selectOptions(picker, "allegro-cpp");
    expect(lastIdeProps.workspaceConfig?.id).toBe("allegro-cpp");
    expect(
      Object.keys(lastIdeProps.workspaceConfig?.files ?? {}),
    ).toContain("/user/allegro-main.cpp");

    await user.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    });
    const payloadArg = putCodingAssignmentAction.mock.calls[0][2] as CodingAssignmentContent;
    expect(payloadArg.Environment.Language).toBe("allegro-cpp");
    expect(payloadArg.Environment.Tools).toBe("clang");
    expect(payloadArg.Environment.LibBundle).toBe("allegro");
    expect(Object.keys(payloadArg.Data.Files)).toContain("/user/allegro-main.cpp");
  });

  it("rejects negative weight client-side — Save disabled + error shown", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-standard"));
    fireEvent.change(screen.getByTestId("standard-weight-0"), {
      target: { value: "-5" },
    });

    expect(
      (await screen.findAllByText(/Weight must be ≥ 0/i)).length,
    ).toBeGreaterThan(0);
    expect(screen.getByTestId("save-button")).toBeDisabled();
    expect(putCodingAssignmentAction).not.toHaveBeenCalled();
  });

  it("submits a valid form and PUTs the v1 CodingAssignmentContent", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-standard"));
    fireEvent.change(screen.getByTestId("standard-stdin-0"), {
      target: { value: "hi" },
    });
    fireEvent.change(screen.getByTestId("standard-stdout-0"), {
      target: { value: "HI" },
    });

    await user.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    });

    const [programIdArg, contentIdArg, payloadArg] = putCodingAssignmentAction.mock.calls[0];
    expect(programIdArg).toBe("course-1");
    expect(contentIdArg).toBe("content-1");
    expect(payloadArg.Type).toBe("coding-assignment");
    expect(payloadArg.Version).toBe(1);
    expect(payloadArg.Environment.Language).toBe("cpp");
    expect(payloadArg.Tests.Public).toHaveLength(1);
    expect(payloadArg.Tests.Public[0]).toMatchObject({
      kind: "standard",
      Stdin: "hi",
      Stdout: "HI",
      Weight: 1,
    });
    // PassingScore was removed from GradingConfig by T4.
    expect(payloadArg.Grading).toEqual({ MaxScore: 100 });

    // Save stays on the page (no redirect) and shows the Saved. indicator.
    expect(await screen.findByText("Saved.")).toBeInTheDocument();
    expect(routerMocks.push).not.toHaveBeenCalled();
  });

  it("autosaves 30s after the last change (debounced) and does not repeat", async () => {
    vi.useFakeTimers();
    try {
      render(<CodingDefinitionEditor {...baseProps} />);

      fireEvent.click(screen.getByTestId("add-standard"));
      fireEvent.change(screen.getByTestId("standard-stdin-0"), {
        target: { value: "hi" },
      });
      fireEvent.change(screen.getByTestId("standard-stdout-0"), {
        target: { value: "HI" },
      });

      // A later change resets the debounce window.
      await act(async () => {
        await vi.advanceTimersByTimeAsync(20_000);
      });
      fireEvent.change(screen.getByTestId("standard-stdin-0"), {
        target: { value: "hi again" },
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(29_999);
      });
      expect(putCodingAssignmentAction).not.toHaveBeenCalled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
      expect(screen.getByText("Saved.")).toBeInTheDocument();
      expect(routerMocks.push).not.toHaveBeenCalled();

      // Success clears dirty: no further saves without new changes.
      await act(async () => {
        await vi.advanceTimersByTimeAsync(60_000);
      });
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it("round-trips an existing v1 CodingAssignmentContent — preserves buckets", async () => {
    const user = userEvent.setup();
    const initialContent: CodingAssignmentContent = {
      Type: "coding-assignment",
      Version: 1,
      Environment: {
        Language: "cpp",
        Tools: "clang",
        LibBundle: null,
        AllowStudentCreateFiles: false,
      },
      Data: {
        Files: {
          "/user/main.cpp": {
            Content: "int main() {}",
            Encoding: "text",
            Visibility: "Public",
            Modifiable: true,
          },
        },
      },
      Tests: {
        Public: [
          {
            kind: "standard",
            Name: "case-a",
            Stdin: "1",
            Stdout: "1",
            Weight: 1,
          },
        ],
        Private: [
          {
            kind: "standard",
            Name: "case-b-secret",
            Stdin: "2",
            Stdout: "2",
            Weight: 1,
          },
        ],
      },
      Grading: { MaxScore: 100 },
    };

    render(
      <CodingDefinitionEditor
        {...baseProps}
        initialContent={initialContent}
      />,
    );

    // No edits — save echoes the round-tripped test buckets.
    await user.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    });
    const payloadArg = putCodingAssignmentAction.mock.calls[0][2] as CodingAssignmentContent;
    const payloadText = JSON.stringify(payloadArg, null, 2);

    // Both standard cases preserved across buckets.
    expect(payloadText.match(/"kind": "standard"/g)).toHaveLength(2);
    expect(payloadText).toContain('"Name": "case-b-secret"');
    expect(payloadText).toContain('"Public":');
    expect(payloadText).toContain('"Private":');
  });

  it("rejects a FunctionalTest with bad function name — Save disabled", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-functional"));
    fireEvent.change(screen.getByTestId("functional-functionName-0"), {
      target: { value: "bad+name" },
    });

    expect(
      (await screen.findAllByText(/Function name must match/i)).length,
    ).toBeGreaterThan(0);
    expect(screen.getByTestId("save-button")).toBeDisabled();
  });

  it("rejects a FunctionalTest with zero cases — mirrors backend at_least_one_case", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-functional"));
    fireEvent.change(screen.getByTestId("functional-functionName-0"), {
      target: { value: "add" },
    });

    expect(
      (await screen.findAllByText(/at least one case/i)).length,
    ).toBeGreaterThan(0);
    expect(screen.getByTestId("save-button")).toBeDisabled();
    expect(putCodingAssignmentAction).not.toHaveBeenCalled();
  });

  it("functional editor: signature parameter + case input — defaults to v1 types only", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    await user.click(screen.getByTestId("add-functional"));
    fireEvent.change(screen.getByTestId("functional-functionName-0"), {
      target: { value: "add" },
    });

    // Add a signature parameter named "a", type Integer.
    await user.click(screen.getByTestId("functional-add-param-0"));
    fireEvent.change(screen.getByTestId("functional-param-name-0-0"), {
      target: { value: "a" },
    });
    await user.click(screen.getByTestId("functional-param-type-0-0"));
    await user.click(await screen.findByRole("option", { name: "Integer" }));

    // Add a case and supply its input value — values live in cases now (T11),
    // not on signature parameters.
    await user.click(screen.getByTestId("functional-add-case-0"));
    fireEvent.change(screen.getByTestId("functional-case-input-value-0-0-0"), {
      target: { value: "2" },
    });

    await user.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
    });
    const payloadArg = putCodingAssignmentAction.mock.calls[0][2] as CodingAssignmentContent;
    const payloadText = JSON.stringify(payloadArg, null, 2);
    expect(payloadText).toContain('"FunctionName": "add"');
    expect(payloadText).toContain('"Type": "integer"');
    expect(payloadText).toContain('"Name": "a"');
    expect(payloadText).toContain('"Content": 2');
  });

  it("create-files toggle lives inside the IDE mount — callback round-trips to next render prop", async () => {
    const user = userEvent.setup();
    render(<CodingDefinitionEditor {...baseProps} />);

    const ideMount = await screen.findByTestId("ide-mount");
    const toggle = await screen.findByTestId("allow-student-create");

    // Toggle moved into the IDE workspace header (no page-level Switch).
    expect(ideMount).toContainElement(toggle);
    expect(screen.queryByRole("switch")).toBeNull();

    expect(lastIdeProps.allowCreateFiles).toBe(false);
    expect(toggle).toHaveTextContent("🔒");

    await user.click(toggle);

    // stale_state guard: callback → state → next render reflects the flip.
    expect(lastIdeProps.allowCreateFiles).toBe(true);
    expect(await screen.findByTestId("allow-student-create")).toHaveTextContent(
      "🔓",
    );
  });

  it("flipping the create-files toggle marks content dirty — autosave PUTs AllowStudentCreateFiles", async () => {
    vi.useFakeTimers();
    try {
      render(<CodingDefinitionEditor {...baseProps} />);

      await act(async () => {
        fireEvent.click(screen.getByTestId("allow-student-create"));
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(30_000);
      });

      expect(putCodingAssignmentAction).toHaveBeenCalledTimes(1);
      const payloadArg = putCodingAssignmentAction.mock.calls[0][2] as CodingAssignmentContent;
      expect(payloadArg.Environment.AllowStudentCreateFiles).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
