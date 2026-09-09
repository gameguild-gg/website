"use client";

import { calendarDatePart, wallClockDate } from "@/lib/date-time-zone";
import { Calendar } from "@game-guild/ui/components/calendar";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@game-guild/ui/components/popover";
import { cn } from "@game-guild/ui/lib/utils";
import { CalendarDays, Clock3 } from "lucide-react";
import * as React from "react";
import type { DateRange } from "react-day-picker";

export type DateTimeRangeValue = {
  start: string;
  end: string;
};

export interface DateTimeRangePickerProps {
  id: string;
  label: string;
  startName: string;
  endName: string;
  timeZoneId: string;
  value?: DateTimeRangeValue;
  defaultValue?: DateTimeRangeValue;
  onValueChange?: (value: DateTimeRangeValue) => void;
  required?: boolean;
  disabled?: boolean;
  className?: string;
}

function timePart(value: string | undefined, fallback: string) {
  return value?.match(/T(\d{2}:\d{2})/)?.[1] ?? fallback;
}

function displayParts(value: string, timeZoneId: string) {
  const date = wallClockDate(value, timeZoneId);
  if (!date) return null;

  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: timeZoneId,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";

  return {
    date: `${part("day")}/${part("month")}/${part("year")}`,
    time: `${part("hour")}:${part("minute")}`,
  };
}

function displayRange(start: string, end: string, timeZoneId: string) {
  const startParts = displayParts(start, timeZoneId);
  const endParts = displayParts(end, timeZoneId);
  if (!startParts || !endParts) return "";

  if (startParts.date === endParts.date) {
    return `${startParts.date} · ${startParts.time}–${endParts.time}`;
  }

  return `${startParts.date} · ${startParts.time} → ${endParts.date} · ${endParts.time}`;
}

function useResponsiveMonthCount() {
  const [monthCount, setMonthCount] = React.useState(1);

  React.useEffect(() => {
    if (typeof window.matchMedia !== "function") return;
    const media = window.matchMedia("(min-width: 768px)");
    const update = () => setMonthCount(media.matches ? 2 : 1);
    update();
    media.addEventListener?.("change", update);
    return () => media.removeEventListener?.("change", update);
  }, []);

  return monthCount;
}

export function DateTimeRangePicker({
  id,
  label,
  startName,
  endName,
  timeZoneId,
  value,
  defaultValue = { start: "", end: "" },
  onValueChange,
  required = false,
  disabled = false,
  className,
}: DateTimeRangePickerProps) {
  const controlled = value !== undefined;
  const committed = value ?? defaultValue;
  const rootRef = React.useRef<HTMLDivElement>(null);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const [internalValue, setInternalValue] = React.useState(defaultValue);
  const current = controlled ? committed : internalValue;
  const [open, setOpen] = React.useState(false);
  const [draftRange, setDraftRange] = React.useState<DateRange | undefined>();
  const [startTime, setStartTime] = React.useState("09:00");
  const [endTime, setEndTime] = React.useState("17:00");
  const monthCount = useResponsiveMonthCount();

  const resetDraft = React.useCallback(() => {
    const start = wallClockDate(current.start, timeZoneId);
    const end = wallClockDate(current.end, timeZoneId);
    setDraftRange(start ? { from: start, to: end } : undefined);
    setStartTime(timePart(current.start, "09:00"));
    setEndTime(timePart(current.end, "17:00"));
  }, [current.end, current.start, timeZoneId]);

  React.useEffect(() => {
    const form = rootRef.current?.closest("form");
    if (!form || controlled) return;
    const reset = () => setInternalValue(defaultValue);
    form.addEventListener("reset", reset);
    return () => form.removeEventListener("reset", reset);
  }, [controlled, defaultValue]);

  function handleOpenChange(nextOpen: boolean) {
    if (nextOpen) resetDraft();
    setOpen(nextOpen);
  }

  function applyRange() {
    if (!draftRange?.from || !draftRange.to) return;
    const startDate = calendarDatePart(draftRange.from, timeZoneId);
    const endDate = calendarDatePart(draftRange.to, timeZoneId);
    if (!startDate || !endDate) return;
    const next = {
      start: `${startDate}T${startTime}`,
      end: `${endDate}T${endTime}`,
    };
    if (!controlled) setInternalValue(next);
    onValueChange?.(next);
    setOpen(false);
  }

  const hasValue = Boolean(current.start && current.end);

  return (
    <div
      ref={rootRef}
      data-slot="date-time-range-picker"
      className={cn("w-full", className)}
    >
      <input
        type="text"
        name={startName}
        value={current.start}
        onChange={() => undefined}
        required={required}
        disabled={disabled}
        tabIndex={-1}
        aria-label={`${label} start value`}
        className="sr-only"
        onInvalid={(event) => {
          event.preventDefault();
          setOpen(true);
          triggerRef.current?.focus();
        }}
      />
      <input
        type="text"
        name={endName}
        value={current.end}
        onChange={() => undefined}
        required={required}
        disabled={disabled}
        tabIndex={-1}
        aria-label={`${label} end value`}
        className="sr-only"
        onInvalid={(event) => {
          event.preventDefault();
          setOpen(true);
          triggerRef.current?.focus();
        }}
      />
      <Popover open={open} onOpenChange={handleOpenChange}>
        <PopoverTrigger asChild>
          <Button
            ref={triggerRef}
            id={id}
            type="button"
            variant="outline"
            disabled={disabled}
            aria-label={label}
            aria-required={required}
            className={cn(
              "h-auto min-h-10 w-full justify-start gap-3 px-3 py-2 text-left font-normal",
              !hasValue && "text-muted-foreground",
            )}
          >
            <CalendarDays className="size-4 shrink-0" aria-hidden="true" />
            <span className="min-w-0 flex-1 truncate">
              {hasValue
                ? displayRange(current.start, current.end, timeZoneId)
                : `Choose ${label.toLowerCase()}`}
            </span>
          </Button>
        </PopoverTrigger>
        <PopoverContent
          align="start"
          className="w-auto max-w-[calc(100vw-2rem)] gap-0 overflow-x-auto p-0"
        >
          <Calendar
            mode="range"
            selected={draftRange}
            defaultMonth={draftRange?.from}
            onSelect={setDraftRange}
            numberOfMonths={monthCount}
            timeZone={timeZoneId}
          />
          <div className="flex flex-col gap-3 border-t p-3 sm:flex-row sm:items-end">
            <Clock3
              className="mb-2 hidden size-4 text-muted-foreground sm:block"
              aria-hidden="true"
            />
            <div className="grid flex-1 gap-1">
              <label className="text-xs font-medium" htmlFor={`${id}-start`}>
                Start time
              </label>
              <Input
                id={`${id}-start`}
                type="time"
                value={startTime}
                onChange={(event) => setStartTime(event.target.value)}
              />
            </div>
            <div className="grid flex-1 gap-1">
              <label className="text-xs font-medium" htmlFor={`${id}-end`}>
                End time
              </label>
              <Input
                id={`${id}-end`}
                type="time"
                value={endTime}
                onChange={(event) => setEndTime(event.target.value)}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setOpen(false)}
              >
                Cancel
              </Button>
              <Button
                type="button"
                size="sm"
                disabled={!draftRange?.from || !draftRange.to}
                onClick={applyRange}
                aria-label={`Apply ${label.toLowerCase()}`}
              >
                Apply
              </Button>
            </div>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
