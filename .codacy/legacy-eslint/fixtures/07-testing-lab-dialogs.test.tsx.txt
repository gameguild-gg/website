import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const actions = vi.hoisted(() => ({
  addTestingParticipant: vi.fn(),
  createTestingLabLocation: vi.fn(),
  createTestingLabRole: vi.fn(),
  createTestingSession: vi.fn(),
  linkTestingSessionProject: vi.fn(),
  submitTestingBuild: vi.fn(),
  updateTestingLabLocation: vi.fn(),
  updateTestingLabRole: vi.fn(),
  updateTestingRequest: vi.fn(),
  updateTestingSession: vi.fn(),
}));

vi.mock("@/lib/testing-lab/actions", () => actions);

import {
  AddTestingParticipantDialog,
  CreateTestingLabRoleDialog,
  CreateTestingLocationDialog,
  CreateTestingSessionDialog,
  EditTestingLabRoleDialog,
  EditTestingLocationDialog,
  EditTestingRequestDialog,
  EditTestingSessionDialog,
  LinkTestingSessionProjectDialog,
  SubmitTestingBuildDialog,
} from "./testing-lab-dialogs";

const project = { id: "project-1", title: "Orbital Runner" };
const request = {
  id: "request-1",
  title: "Vertical slice",
  status: "Open" as const,
  description: "Test the onboarding",
  startDate: "2026-10-01T09:30:00.000Z",
  endDate: "invalid-date",
  maxTesters: 12,
};
const location = {
  id: "location-1",
  name: "Remote lab",
  status: "Active" as const,
  isVirtual: true,
  virtualUrl: "https://meet.example.test/lab",
  maxTestersCapacity: 20,
  maxProjectsCapacity: 5,
};
const session = {
  id: "session-1",
  sessionName: "Friday playtest",
  status: "Scheduled" as const,
  sessionDate: "2026-10-03",
  startTime: "2026-10-03T09:00:00.000Z",
  endTime: "2026-10-03T12:00:00.000Z",
  locationId: "location-1",
  maxTesters: 20,
  maxProjects: 5,
};
const role = {
  id: "role-1",
  name: "Facilitator",
  description: "Runs sessions",
  permissions: { canViewSessions: true, canEditSessions: true },
};

describe("Testing Lab dialogs", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    Object.values(actions).forEach((action) =>
      action.mockResolvedValue({ success: true, data: null, message: "Saved." }),
    );
  });

  afterEach(() => cleanup());

  it("opens the location form from its composed trigger", () => {
    render(<CreateTestingLocationDialog />);
    fireEvent.click(screen.getByRole("button", { name: "New location" }));
    expect(screen.getByRole("dialog", { name: "Create testing location" })).toBeInTheDocument();
    expect(screen.getByLabelText("Location name")).toBeInTheDocument();
  });

  it.each([
    { name: "request", trigger: "New request", dialog: "Submit a build for testing", element: <SubmitTestingBuildDialog projects={[project]} /> },
    {
      name: "participant",
      trigger: "Add participant",
      dialog: "Add a participant",
      element: <AddTestingParticipantDialog requestId="request-1" members={[{ id: "user-1", displayName: "Alex" }]} />,
    },
    { name: "project link", trigger: "Link project", dialog: "Link a project", element: <LinkTestingSessionProjectDialog sessionId="session-1" projects={[project]} /> },
    { name: "session", trigger: "Schedule session", dialog: "Schedule a testing session", element: <CreateTestingSessionDialog requests={[request]} locations={[location]} /> },
    { name: "role", trigger: "New role", dialog: "Create Testing Lab role", element: <CreateTestingLabRoleDialog /> },
    { name: "request edit", trigger: "Edit request", dialog: "Edit testing request", element: <EditTestingRequestDialog request={request} /> },
    { name: "session edit", trigger: "Edit session", dialog: "Edit testing session", element: <EditTestingSessionDialog session={session} locations={[location]} /> },
    { name: "location edit", trigger: "Edit", dialog: "Edit Remote lab", element: <EditTestingLocationDialog location={location} /> },
    { name: "role edit", trigger: "Edit", dialog: "Edit Facilitator", element: <EditTestingLabRoleDialog role={role} /> },
  ])("opens the $name dialog with its expected heading", ({ trigger, dialog, element }) => {
    render(element);
    fireEvent.click(screen.getByRole("button", { name: trigger }));
    expect(screen.getByRole("dialog", { name: dialog })).toBeInTheDocument();
  });

  it("disables project linking when no approved projects are available", () => {
    render(<LinkTestingSessionProjectDialog sessionId="session-1" projects={[]} />);
    expect(screen.getByRole("button", { name: "Link project" })).toBeDisabled();
  });

  it("submits form data and presents server validation errors", async () => {
    const user = userEvent.setup();
    actions.createTestingLabLocation.mockResolvedValueOnce({ success: false, error: "Location already exists." });
    render(<CreateTestingLocationDialog />);
    await user.click(screen.getByRole("button", { name: "New location" }));
    await user.type(screen.getByLabelText("Location name"), "Remote lab");
    await user.click(screen.getByRole("button", { name: "Create location" }));

    expect(await screen.findByText("Location already exists.")).toBeInTheDocument();
    const submitted = actions.createTestingLabLocation.mock.calls[0]?.[0] as FormData;
    expect(submitted.get("name")).toBe("Remote lab");
  });

  it("resets and closes the dialog after a successful action", async () => {
    const user = userEvent.setup();
    const originalSetTimeout = window.setTimeout.bind(window);
    const timer = vi.spyOn(window, "setTimeout").mockImplementation((handler, timeout, ...args) =>
      originalSetTimeout(handler, timeout === 500 ? 0 : timeout, ...args),
    );
    render(<CreateTestingLocationDialog />);
    await user.click(screen.getByRole("button", { name: "New location" }));
    await user.type(screen.getByLabelText("Location name"), "Remote lab");
    await user.click(screen.getByRole("button", { name: "Create location" }));

    await waitFor(() => expect(actions.createTestingLabLocation).toHaveBeenCalledOnce());
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(timer).toHaveBeenCalledWith(expect.any(Function), 500);
    timer.mockRestore();
  });

  it("normalizes valid non-ISO display dates for edit controls", async () => {
    const user = userEvent.setup();
    render(
      <EditTestingSessionDialog
        session={{
          ...session,
          sessionDate: "October 3, 2026 12:00:00 GMT",
          startTime: "October 3, 2026 09:00:00 GMT",
          endTime: "October 3, 2026 12:00:00 GMT",
        }}
        locations={[location]}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Edit session" }));

    expect(document.querySelector<HTMLInputElement>('input[name="sessionDate"]')?.value).toBe("2026-10-03");
    expect(document.querySelector<HTMLInputElement>('input[name="startTime"]')?.value).toBe("2026-10-03T09:00");
  });

  it("converts unexpected action failures into an actionable dialog error", async () => {
    const user = userEvent.setup();
    actions.createTestingLabLocation.mockRejectedValueOnce(new Error("Network unavailable"));
    render(<CreateTestingLocationDialog />);
    await user.click(screen.getByRole("button", { name: "New location" }));
    await user.type(screen.getByLabelText("Location name"), "Remote lab");
    await user.click(screen.getByRole("button", { name: "Create location" }));
    expect(await screen.findByText("Network unavailable")).toBeInTheDocument();
  });

  it("uses a safe fallback for non-Error action failures and clears it when cancelled", async () => {
    const user = userEvent.setup();
    actions.createTestingLabLocation.mockRejectedValueOnce("offline");
    render(<CreateTestingLocationDialog />);
    await user.click(screen.getByRole("button", { name: "New location" }));
    await user.type(screen.getByLabelText("Location name"), "Remote lab");
    await user.click(screen.getByRole("button", { name: "Create location" }));
    expect(await screen.findByText("The Testing Lab operation failed.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: "New location" }));
    expect(screen.queryByText("The Testing Lab operation failed.")).not.toBeInTheDocument();
  });

  it("falls back from participant names to email and then an unknown label", async () => {
    const user = userEvent.setup();
    render(
      <AddTestingParticipantDialog
        requestId="request-1"
        members={[
          { id: "user-email", email: "member@example.test" },
          { id: "user-unknown" },
        ]}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Add participant" }));
    await user.click(screen.getByLabelText("Member"));

    expect(
      await screen.findByRole("option", { name: "member@example.test" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Unknown member" }),
    ).toBeInTheDocument();
  });

  it("renders empty request fields and dates without inventing values", async () => {
    const user = userEvent.setup();
    render(
      <EditTestingRequestDialog
        request={{
          id: "request-empty",
          title: "Empty request",
          status: "Draft",
          description: undefined,
          downloadUrl: undefined,
          startDate: undefined,
          endDate: null,
          maxTesters: undefined,
          instructionsContent: undefined,
          feedbackFormContent: undefined,
        }}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Edit request" }));

    expect(screen.getByLabelText("Objective")).toHaveValue("");
    expect(screen.getByLabelText("Build URL")).toHaveValue("");
    expect(screen.getByLabelText("Starts")).toHaveValue("");
    expect(screen.getByLabelText("Ends")).toHaveValue("");
    expect(screen.getByLabelText("Tester capacity")).toHaveValue(null);
    expect(screen.getByLabelText("Instructions")).toHaveValue("");
    expect(screen.getByLabelText("Feedback prompt")).toHaveValue("");
  });

  it("uses safe session defaults when location and capacities are absent", async () => {
    const user = userEvent.setup();
    render(
      <EditTestingSessionDialog
        session={{
          ...session,
          locationId: undefined,
          maxTesters: undefined,
          maxProjects: undefined,
        }}
        locations={[]}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Edit session" }));

    expect(document.querySelector<HTMLInputElement>('input[name="maxTesters"]')).toHaveValue(1);
    expect(document.querySelector<HTMLInputElement>('input[name="maxProjects"]')).toHaveValue(1);
  });

  it("renders role defaults when optional role metadata is missing", async () => {
    const user = userEvent.setup();
    const { unmount } = render(
      <EditTestingLabRoleDialog
        role={{
          id: undefined,
          name: "Observer",
          description: undefined,
          permissions: undefined,
        }}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Edit" }));
    expect(document.querySelector<HTMLInputElement>('input[name="idOrName"]')).toHaveValue("Observer");
    expect(screen.getByRole("textbox", { name: "Description" })).toHaveValue("");
    unmount();

    render(
      <EditTestingLabRoleDialog
        role={{
          id: undefined,
          name: undefined,
          description: undefined,
          permissions: undefined,
        }}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Edit" }));
    expect(document.querySelector<HTMLInputElement>('input[name="idOrName"]')).toHaveValue("");
    expect(screen.getByRole("textbox", { name: "Role name" })).toHaveValue("");
  });
});
