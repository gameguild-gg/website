import { describe, expect, it } from "vitest";

import { SOCIAL_NAVIGATION } from "./social-sidebar";

describe("social sidebar navigation", () => {
  it("exposes only implemented destinations and routes Testing Lab through Workspace", () => {
    expect(SOCIAL_NAVIGATION.map((item) => item.label)).toEqual([
      "Home",
      "Explore",
      "Testing Lab",
      "Saved",
    ]);
    expect(SOCIAL_NAVIGATION.find((item) => item.label === "Testing Lab")?.href).toBe(
      "/workspace/testing-lab",
    );
    expect(SOCIAL_NAVIGATION.some((item) => item.label === "Messages")).toBe(false);
  });
});
