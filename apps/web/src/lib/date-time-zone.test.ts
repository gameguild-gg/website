import { describe, expect, it } from "vitest";

import { formatWallClockInTimeZone, wallClockToUtcIso } from "./date-time-zone";

describe("date time zone helpers", () => {
  it("converts an event wall clock to UTC", () => {
    expect(wallClockToUtcIso("2026-08-01T09:00", "America/Sao_Paulo")).toBe(
      "2026-08-01T12:00:00.000Z",
    );
  });

  it("keeps the local hour across daylight-saving offsets", () => {
    expect(wallClockToUtcIso("2026-03-01T10:00", "America/New_York")).toBe(
      "2026-03-01T15:00:00.000Z",
    );
    expect(wallClockToUtcIso("2026-03-08T10:00", "America/New_York")).toBe(
      "2026-03-08T14:00:00.000Z",
    );
  });

  it("rejects nonexistent local times during a daylight-saving jump", () => {
    expect(
      wallClockToUtcIso("2026-03-08T02:30", "America/New_York"),
    ).toBeNull();
  });

  it("formats instants back into the selected event timezone", () => {
    expect(
      formatWallClockInTimeZone(
        new Date("2026-08-01T12:00:00.000Z"),
        "America/Sao_Paulo",
      ),
    ).toBe("2026-08-01T09:00");
  });
});
