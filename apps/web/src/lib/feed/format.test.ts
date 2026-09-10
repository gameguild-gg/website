import { describe, expect, it } from "vitest";
import { formatSocialDate, formatSocialDateTime } from "./format";

describe("social date formatting", () => {
  it("renders the same calendar value independently of the machine time zone", () => {
    expect(formatSocialDateTime("2026-09-06T15:00:00Z")).toBe(
      "Sep 6, 2026, 3:00 PM UTC",
    );
    expect(formatSocialDate("2026-09-06T23:30:00-03:00")).toBe("Sep 7, 2026");
  });

  it("handles invalid values without crashing the feed", () => {
    expect(formatSocialDate("not-a-date")).toBe("");
    expect(formatSocialDateTime("not-a-date")).toBe("Schedule pending");
  });
});
