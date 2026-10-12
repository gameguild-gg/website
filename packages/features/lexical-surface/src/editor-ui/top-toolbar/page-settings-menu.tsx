/**
 * PageSettingsDropDown — toolbar control that lets the user pick the
 * page size, orientation, and margins of the editable area. Wires up to
 * `ToolbarContext.pageSettings`.
 */
"use client";

import * as React from "react";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@game-guild/ui/components/popover";
import { cn } from "@game-guild/ui/lib/utils";
import { ChevronDownIcon, ChevronUpIcon, PageIcon } from "../../icons";
import {
  PAGE_MARGIN_LABELS,
  PAGE_ORIENTATION_LABELS,
  PAGE_SIZES,
  type PageMargin,
  type PageOrientation,
  type PageSizeId,
} from "../../features/page";
import { useToolbarState } from "./toolbar-context";

function Section({
  title,
  open,
  onToggle,
  children,
}: {
  title: string;
  open: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="border-b border-gray-200 dark:border-gray-700 last:border-b-0">
      <button
        type="button"
        onClick={onToggle}
        className={cn(
          "w-full flex items-center justify-between px-3 py-2 text-sm font-medium",
          "text-blue-600 dark:text-blue-400 hover:bg-gray-50 dark:hover:bg-gray-800",
        )}
      >
        <span>{title}</span>
        {open ? (
          <ChevronUpIcon className="w-4 h-4 opacity-60" />
        ) : (
          <ChevronDownIcon className="w-4 h-4 opacity-60" />
        )}
      </button>
      {open && <div className="pb-1">{children}</div>}
    </div>
  );
}

function OptionRow({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "w-full text-left px-6 py-1.5 text-sm",
        "text-gray-800 dark:text-gray-200",
        active
          ? "bg-blue-50 dark:bg-blue-950/40 font-medium"
          : "hover:bg-gray-50 dark:hover:bg-gray-800",
      )}
    >
      {children}
    </button>
  );
}

export function PageSettingsDropDown({ disabled }: { disabled?: boolean }) {
  const { pageSettings, setPageSettings } = useToolbarState();
  const [open, setOpen] = React.useState(false);
  const [sizeOpen, setSizeOpen] = React.useState(true);
  const [orientationOpen, setOrientationOpen] = React.useState(true);
  const [marginOpen, setMarginOpen] = React.useState(true);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <button
            type="button"
            disabled={disabled}
            aria-label="Page settings"
            title="Page settings"
            className={cn(
              "inline-flex items-center gap-1 h-8 px-2 rounded text-sm",
              "hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-40 disabled:pointer-events-none",
            )}
          >
            <PageIcon className="w-4 h-4" />
            <ChevronDownIcon className="w-3 h-3 opacity-60" />
          </button>
        }
      />
      <PopoverContent
        align="start"
        sideOffset={4}
        className="w-[280px] p-0 max-h-[60vh] overflow-y-auto"
      >
        <Section
          title="Page size"
          open={sizeOpen}
          onToggle={() => setSizeOpen((v) => !v)}
        >
          {PAGE_SIZES.map((size) => (
            <OptionRow
              key={size.id}
              active={pageSettings.size === size.id}
              onClick={() =>
                setPageSettings({
                  ...pageSettings,
                  size: size.id as PageSizeId,
                })
              }
            >
              {size.label}
            </OptionRow>
          ))}
        </Section>
        <Section
          title="Orientation"
          open={orientationOpen}
          onToggle={() => setOrientationOpen((v) => !v)}
        >
          {(Object.keys(PAGE_ORIENTATION_LABELS) as PageOrientation[]).map(
            (o) => (
              <OptionRow
                key={o}
                active={pageSettings.orientation === o}
                onClick={() =>
                  { setPageSettings({ ...pageSettings, orientation: o }); }
                }
              >
                {PAGE_ORIENTATION_LABELS[o]}
              </OptionRow>
            ),
          )}
        </Section>
        <Section
          title="Margins"
          open={marginOpen}
          onToggle={() => setMarginOpen((v) => !v)}
        >
          {(Object.keys(PAGE_MARGIN_LABELS) as PageMargin[]).map((m) => (
            <OptionRow
              key={m}
              active={pageSettings.margin === m}
              onClick={() => setPageSettings({ ...pageSettings, margin: m })}
            >
              {PAGE_MARGIN_LABELS[m]}
            </OptionRow>
          ))}
        </Section>
      </PopoverContent>
    </Popover>
  );
}
