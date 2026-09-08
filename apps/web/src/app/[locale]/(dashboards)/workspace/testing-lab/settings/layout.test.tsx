import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/i18n/navigation", () => ({
  usePathname: () => "/workspace/testing-lab/settings/locations",
  Link: ({
    children,
    href,
    ...props
  }: {
    children: ReactNode;
    href: string;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

import TestingLabSettingsLayout from "./layout";

vi.mock("@/lib/require-dashboard-capability", () => ({
  requireAnyDashboardCapability: vi.fn().mockResolvedValue({
    capabilities: ["TestingLab.ManageSettings", "TestingLab.ViewAnalytics"],
  }),
}));

describe("TestingLabSettingsLayout", () => {
  it("groups general, location, and access settings without horizontal overflow", async () => {
    render(await TestingLabSettingsLayout({ children: <div>Settings content</div> }));

    const navigation = screen.getByRole("navigation", {
      name: "Testing Lab settings",
    });
    expect(navigation).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "General" })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/settings/general",
    );
    expect(screen.getByRole("link", { name: "Analytics" })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/settings/analytics",
    );
    expect(screen.getByRole("link", { name: "Locations" })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/settings/locations",
    );
    expect(screen.getByRole("link", { name: "Access" })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/settings/access",
    );
  });
});
