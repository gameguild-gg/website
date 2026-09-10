const DATE_FORMATTER = new Intl.DateTimeFormat("en-US", {
  dateStyle: "medium",
  timeZone: "UTC",
});

const DATE_TIME_FORMATTER = new Intl.DateTimeFormat("en-US", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "UTC",
});

export function formatSocialDate(value: string | number | Date) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : DATE_FORMATTER.format(date);
}

export function formatSocialDateTime(value: string | number | Date) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Schedule pending"
    : `${DATE_TIME_FORMATTER.format(date)} UTC`;
}
