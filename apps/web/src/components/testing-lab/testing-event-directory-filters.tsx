"use client";

import { usePathname, useRouter } from "@/i18n/navigation";
import type { TestingLabTestingEventStatus } from "@game-guild/client";
import { Button } from "@game-guild/ui/components/button";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from "@game-guild/ui/components/input-group";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { ArrowRight, ListFilter, Search, X } from "lucide-react";
import { type FormEvent, useState, useTransition } from "react";

type DirectoryFilter = TestingLabTestingEventStatus | "all" | "archived";

const filterOptions: Array<{ value: DirectoryFilter; label: string }> = [
  { value: "all", label: "All statuses" },
  { value: "Draft", label: "Draft" },
  { value: "ApplicationsOpen", label: "Applications open" },
  { value: "ApplicationsClosed", label: "Applications closed" },
  { value: "Scheduled", label: "Scheduled" },
  { value: "Active", label: "Active" },
  { value: "Completed", label: "Completed" },
  { value: "Cancelled", label: "Cancelled" },
  { value: "archived", label: "Archived" },
];

export function TestingEventDirectoryFilters({
  search,
  status,
  archived = false,
}: {
  search?: string;
  status?: TestingLabTestingEventStatus;
  archived?: boolean;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [query, setQuery] = useState(search ?? "");
  const [isPending, startTransition] = useTransition();
  const selectedFilter: DirectoryFilter = archived ? "archived" : status ?? "all";
  const hasFilters = Boolean(search || status || archived);

  function navigate(nextSearch: string, nextFilter: DirectoryFilter) {
    const params = new URLSearchParams();
    const normalizedSearch = nextSearch.trim();

    if (normalizedSearch) params.set("q", normalizedSearch);
    if (nextFilter === "archived") params.set("archived", "true");
    else if (nextFilter !== "all") params.set("status", nextFilter);

    const suffix = params.toString();
    const destination = suffix ? `${pathname}?${suffix}` : pathname;
    startTransition(() => router.replace(destination));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    navigate(query, selectedFilter);
  }

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      <form onSubmit={handleSubmit} className="min-w-0 flex-1 sm:max-w-md">
        <InputGroup>
          <InputGroupInput
            type="search"
            aria-label="Search testing events"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search events"
            disabled={isPending}
          />
          <InputGroupAddon>
            <Search aria-hidden="true" />
          </InputGroupAddon>
          <InputGroupAddon align="inline-end">
            <InputGroupButton
              type="submit"
              size="icon-xs"
              aria-label="Run event search"
              disabled={isPending}
            >
              <ArrowRight aria-hidden="true" />
            </InputGroupButton>
          </InputGroupAddon>
        </InputGroup>
      </form>

      <div className="flex items-center gap-1">
        <Select
          items={filterOptions}
          value={selectedFilter}
          onValueChange={(value) => navigate(query, value as DirectoryFilter)}
          disabled={isPending}
        >
          <SelectTrigger
            size="default"
            className="w-full sm:w-48"
            aria-label="Filter testing events by status"
          >
            <ListFilter aria-hidden="true" />
            <SelectValue />
          </SelectTrigger>
          <SelectContent alignItemWithTrigger={false}>
            <SelectGroup>
              {filterOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectGroup>
          </SelectContent>
        </Select>

        {hasFilters ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="Clear event filters"
            title="Clear filters"
            disabled={isPending}
            onClick={() => {
              setQuery("");
              navigate("", "all");
            }}
          >
            <X aria-hidden="true" />
          </Button>
        ) : null}
      </div>
    </div>
  );
}
