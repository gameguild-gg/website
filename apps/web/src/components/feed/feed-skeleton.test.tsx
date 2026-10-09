import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { FeedSkeleton } from "./feed-skeleton";

describe("FeedSkeleton", () => {
  it("mounts the loading skeleton with 3 post placeholders", () => {
    render(<FeedSkeleton />);

    const root = screen.getByTestId("feed-loading-skeleton");
    expect(root).toBeInTheDocument();
    expect(root.getAttribute("role")).toBe("status");
    expect(screen.getAllByTestId("feed-skeleton-post")).toHaveLength(3);
  });

  it("mirrors the feed column structure: tab bar, stories, composer, posts", () => {
    const { container } = render(<FeedSkeleton />);

    // Sticky tab bar placeholder (h-14)
    const tabBar = container.querySelector(".sticky.h-14");
    expect(tabBar).not.toBeNull();
    // Stories strip: 6 circle placeholders
    const circles = container.querySelectorAll(".h-24 .rounded-full");
    expect(circles).toHaveLength(6);
    // Composer card placeholder (h-20)
    expect(container.querySelector(".h-20")).not.toBeNull();
  });
});
