import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
}));

vi.mock("@/i18n/navigation", () => ({
  usePathname: () => "/workspace/testing-lab/events",
  useRouter: () => ({ replace: mocks.replace }),
}));

import { TestingEventDirectoryFilters } from "./testing-event-directory-filters";

describe("TestingEventDirectoryFilters", () => {
  beforeEach(() => mocks.replace.mockReset());

  it("combines search and status in a compact filter toolbar", async () => {
    const user = userEvent.setup();
    render(
      <TestingEventDirectoryFilters
        search="campus"
        status="ApplicationsOpen"
      />,
    );

    expect(screen.getByRole("searchbox", { name: "Search testing events" })).toHaveValue("campus");
    const statusFilter = screen.getByRole("combobox", {
      name: "Filter testing events by status",
    });
    expect(statusFilter).toHaveTextContent("Applications open");

    await user.click(statusFilter);
    await user.click(screen.getByRole("option", { name: "Active" }));

    expect(mocks.replace).toHaveBeenCalledWith(
      "/workspace/testing-lab/events?q=campus&status=Active",
    );
  });

  it("keeps archived inside the status selector and clears every filter", async () => {
    const user = userEvent.setup();
    render(<TestingEventDirectoryFilters search="old" archived />);

    expect(
      screen.getByRole("combobox", { name: "Filter testing events by status" }),
    ).toHaveTextContent("Archived");

    await user.click(screen.getByRole("button", { name: "Clear event filters" }));

    expect(mocks.replace).toHaveBeenCalledWith(
      "/workspace/testing-lab/events",
    );
  });

  it("runs the search without a detached text button", async () => {
    const user = userEvent.setup();
    render(<TestingEventDirectoryFilters />);

    await user.type(
      screen.getByRole("searchbox", { name: "Search testing events" }),
      "remote review",
    );
    await user.click(screen.getByRole("button", { name: "Run event search" }));

    expect(mocks.replace).toHaveBeenCalledWith(
      "/workspace/testing-lab/events?q=remote+review",
    );
    expect(screen.queryByRole("button", { name: "Search" })).not.toBeInTheDocument();
  });
});
