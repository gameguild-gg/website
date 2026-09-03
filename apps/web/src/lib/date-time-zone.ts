const wallClockPattern =
  /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d{1,3})?)?$/;

type WallClockParts = {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
};

function parseWallClock(value: string): WallClockParts | null {
  const match = value.match(wallClockPattern);
  if (!match) return null;

  const parts: WallClockParts = {
    year: Number(match[1]),
    month: Number(match[2]),
    day: Number(match[3]),
    hour: Number(match[4]),
    minute: Number(match[5]),
    second: Number(match[6] ?? 0),
  };
  const check = new Date(
    Date.UTC(
      parts.year,
      parts.month - 1,
      parts.day,
      parts.hour,
      parts.minute,
      parts.second,
    ),
  );

  return check.getUTCFullYear() === parts.year &&
    check.getUTCMonth() === parts.month - 1 &&
    check.getUTCDate() === parts.day &&
    check.getUTCHours() === parts.hour &&
    check.getUTCMinutes() === parts.minute &&
    check.getUTCSeconds() === parts.second
    ? parts
    : null;
}

function zonedParts(date: Date, timeZone: string): WallClockParts | null {
  try {
    const formatter = new Intl.DateTimeFormat("en-CA", {
      timeZone,
      calendar: "gregory",
      numberingSystem: "latn",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hourCycle: "h23",
    });
    const values = Object.fromEntries(
      formatter
        .formatToParts(date)
        .filter(({ type }) => type !== "literal")
        .map(({ type, value }) => [type, value]),
    );
    return {
      year: Number(values.year),
      month: Number(values.month),
      day: Number(values.day),
      hour: Number(values.hour),
      minute: Number(values.minute),
      second: Number(values.second),
    };
  } catch {
    return null;
  }
}

function partsAsUtc(parts: WallClockParts) {
  return Date.UTC(
    parts.year,
    parts.month - 1,
    parts.day,
    parts.hour,
    parts.minute,
    parts.second,
  );
}

function sameWallClock(left: WallClockParts, right: WallClockParts) {
  return (
    left.year === right.year &&
    left.month === right.month &&
    left.day === right.day &&
    left.hour === right.hour &&
    left.minute === right.minute &&
    left.second === right.second
  );
}

function pad(value: number) {
  return String(value).padStart(2, "0");
}

export function isSupportedTimeZone(timeZone: string) {
  if (!timeZone.trim()) return false;
  try {
    new Intl.DateTimeFormat("en", { timeZone }).format();
    return true;
  } catch {
    return false;
  }
}

/** Converts a timezone-local wall clock value into its unique UTC instant. */
export function wallClockToUtcIso(value: string, timeZone: string) {
  const target = parseWallClock(value);
  if (!target || !isSupportedTimeZone(timeZone)) return null;

  const targetAsUtc = partsAsUtc(target);
  let candidate = targetAsUtc;

  // Offsets can change near DST boundaries. Re-evaluating the displayed wall
  // clock converges on the correct instant without relying on the server zone.
  for (let attempt = 0; attempt < 4; attempt += 1) {
    const displayed = zonedParts(new Date(candidate), timeZone);
    if (!displayed) return null;
    const adjustment = targetAsUtc - partsAsUtc(displayed);
    candidate += adjustment;
    if (adjustment === 0) break;
  }

  const resolved = zonedParts(new Date(candidate), timeZone);
  if (!resolved || !sameWallClock(resolved, target)) return null;
  return new Date(candidate).toISOString();
}

export function formatWallClockInTimeZone(date: Date, timeZone: string) {
  const parts = zonedParts(date, timeZone);
  if (!parts) return "";
  return `${parts.year}-${pad(parts.month)}-${pad(parts.day)}T${pad(parts.hour)}:${pad(parts.minute)}`;
}

export function wallClockDate(value: string, timeZone: string) {
  const iso = wallClockToUtcIso(value, timeZone);
  return iso ? new Date(iso) : undefined;
}

export function calendarDatePart(date: Date, timeZone: string) {
  const parts = zonedParts(date, timeZone);
  return parts ? `${parts.year}-${pad(parts.month)}-${pad(parts.day)}` : "";
}

export function timeZoneOffsetLabel(timeZone: string, date = new Date()) {
  try {
    const value = new Intl.DateTimeFormat("en", {
      timeZone,
      timeZoneName: "longOffset",
    })
      .formatToParts(date)
      .find(({ type }) => type === "timeZoneName")?.value;
    return value?.replace("GMT", "UTC") ?? "UTC";
  } catch {
    return "UTC";
  }
}

export function supportedTimeZones() {
  const supportedValuesOf = (
    Intl as typeof Intl & {
      supportedValuesOf?: (key: "timeZone") => string[];
    }
  ).supportedValuesOf;
  const zones = supportedValuesOf?.("timeZone") ?? [];
  return Array.from(new Set(["UTC", ...zones]));
}
