"use client";

import { supportedTimeZones, timeZoneOffsetLabel } from "@/lib/date-time-zone";
import { Button } from "@game-guild/ui/components/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@game-guild/ui/components/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@game-guild/ui/components/popover";
import { ChevronsUpDown } from "lucide-react";
import * as React from "react";

export interface TimeZoneComboboxProps {
  id: string;
  name?: string;
  value: string;
  onValueChange: (timeZone: string) => void;
  disabled?: boolean;
}

export function TimeZoneCombobox({
  id,
  name = "timeZoneId",
  value,
  onValueChange,
  disabled = false,
}: TimeZoneComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const zones = React.useMemo(
    () =>
      Array.from(
        new Set(open ? [value, ...supportedTimeZones()] : [value, "UTC"]),
      ),
    [open, value],
  );

  return (
    <>
      <input type="hidden" name={name} value={value} />
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            id={id}
            type="button"
            variant="outline"
            role="combobox"
            aria-label="Time zone"
            aria-expanded={open}
            disabled={disabled}
            className="h-10 w-full min-w-0 justify-between font-normal"
          >
            <span className="truncate">
              {timeZoneOffsetLabel(value)} · {value}
            </span>
            <ChevronsUpDown
              className="size-4 shrink-0 text-muted-foreground"
              aria-hidden="true"
            />
          </Button>
        </PopoverTrigger>
        <PopoverContent
          align="end"
          className="w-96 max-w-[calc(100vw-2rem)] p-0"
        >
          <Command>
            <CommandInput placeholder="Search time zones…" />
            <CommandList>
              <CommandEmpty>No time zone found.</CommandEmpty>
              <CommandGroup>
                {zones.map((zone) => (
                  <CommandItem
                    key={zone}
                    value={`${zone} ${timeZoneOffsetLabel(zone)}`}
                    data-checked={zone === value}
                    onSelect={() => {
                      onValueChange(zone);
                      setOpen(false);
                    }}
                  >
                    <span className="w-16 shrink-0 text-muted-foreground">
                      {timeZoneOffsetLabel(zone)}
                    </span>
                    <span>{zone}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </>
  );
}
