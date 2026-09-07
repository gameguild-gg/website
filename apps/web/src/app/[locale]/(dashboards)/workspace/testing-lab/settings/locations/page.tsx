import { TestingLabConfirmAction } from "@/components/testing-lab/testing-lab-confirm-action";
import {
  CreateTestingLocationDialog,
  EditTestingLocationDialog,
} from "@/components/testing-lab/testing-lab-dialogs";
import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import {
  TestingLabAccessIssues,
  TestingLabEmptyState,
} from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import {
  deleteTestingLabLocation,
  restoreTestingLabLocation,
} from "@/lib/testing-lab/actions";
import {
  filterTestingLabLocations,
  getTestingLabLocations,
  normalizeTestingLocationStatus,
  type TestingLocationSummary,
} from "@/lib/testing-lab";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@game-guild/ui/components/table";
import {
  Building2,
  Gamepad2,
  Globe2,
  Mail,
  MapPin,
  Search,
  SlidersHorizontal,
  Users,
} from "lucide-react";

interface LocationSearchParams {
  q?: string;
  status?: "all" | "active" | "maintenance" | "inactive" | "archived";
  mode?: "all" | "physical" | "remote";
}

function getLocationPlace(location: TestingLocationSummary) {
  if (location.isVirtual)
    return location.virtualUrl ?? "Remote connection not configured";
  return (
    [
      location.address,
      location.city,
      location.state,
      location.postalCode,
      location.country,
    ]
      .filter(Boolean)
      .join(", ") || "Address not configured"
  );
}

function LocationActions({ location }: { location: TestingLocationSummary }) {
  if (location.isDeleted) {
    return (
      <TestingLabConfirmAction
        action={restoreTestingLabLocation}
        fields={{ locationId: location.id }}
        label="Restore"
        title="Restore this location?"
        description="The location becomes available for future scheduling again."
        confirmLabel="Restore location"
        intent="restore"
      />
    );
  }

  return (
    <div className="flex items-center justify-end gap-2">
      <EditTestingLocationDialog location={location} />
      <TestingLabConfirmAction
        action={deleteTestingLabLocation}
        fields={{ locationId: location.id }}
        label="Archive"
        title="Archive this location?"
        description="Upcoming or active sessions must be moved before this location can be archived."
        confirmLabel="Archive location"
        intent="archive"
      />
    </div>
  );
}

export default async function TestingLabLocationsPage({
  searchParams,
}: {
  searchParams: Promise<LocationSearchParams>;
}) {
  const params = await searchParams;
  const directory = await getTestingLabLocations();
  const locations = filterTestingLabLocations(directory.locations, {
    q: params.q,
    status: params.status ?? "all",
    mode: params.mode ?? "all",
  });
  const activeCount = directory.locations.filter(
    (location) =>
      !location.isDeleted &&
      normalizeTestingLocationStatus(location.status) === "Active",
  ).length;
  const remoteCount = directory.locations.filter(
    (location) => !location.isDeleted && location.isVirtual,
  ).length;
  const archivedCount = directory.locations.filter(
    (location) => location.isDeleted,
  ).length;

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={MapPin}
        title="Testing locations"
        description="Manage physical rooms and remote labs used by Testing Lab schedules."
        actions={<CreateTestingLocationDialog />}
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />

      <dl className="grid grid-cols-3 divide-x rounded-md border bg-muted/15">
        <div className="px-4 py-3">
          <dt className="text-xs text-muted-foreground">Active</dt>
          <dd className="mt-1 text-lg font-semibold">{activeCount}</dd>
        </div>
        <div className="px-4 py-3">
          <dt className="text-xs text-muted-foreground">Remote</dt>
          <dd className="mt-1 text-lg font-semibold">{remoteCount}</dd>
        </div>
        <div className="px-4 py-3">
          <dt className="text-xs text-muted-foreground">Archived</dt>
          <dd className="mt-1 text-lg font-semibold">{archivedCount}</dd>
        </div>
      </dl>

      <form
        method="get"
        className="grid gap-3 rounded-md border p-3 md:grid-cols-[minmax(15rem,1fr)_12rem_12rem_auto_auto]"
      >
        <div className="relative">
          <Search
            aria-hidden="true"
            className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <Input
            name="q"
            defaultValue={params.q}
            className="pl-9"
            placeholder="Search name, city, address, or contact"
            aria-label="Search locations"
          />
        </div>
        <Select name="status" defaultValue={params.status ?? "all"}>
          <SelectTrigger aria-label="Location status">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="active">Active</SelectItem>
            <SelectItem value="maintenance">Maintenance</SelectItem>
            <SelectItem value="inactive">Inactive</SelectItem>
            <SelectItem value="archived">Archived</SelectItem>
          </SelectContent>
        </Select>
        <Select name="mode" defaultValue={params.mode ?? "all"}>
          <SelectTrigger aria-label="Delivery mode">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All modes</SelectItem>
            <SelectItem value="physical">Physical</SelectItem>
            <SelectItem value="remote">Remote</SelectItem>
          </SelectContent>
        </Select>
        <Button type="submit" variant="outline">
          <SlidersHorizontal aria-hidden="true" className="mr-2 size-4" />
          Apply
        </Button>
        <Button asChild type="button" variant="ghost">
          <Link href="/workspace/testing-lab/settings/locations">Reset</Link>
        </Button>
      </form>

      {locations.length === 0 ? (
        <TestingLabEmptyState
          title={
            directory.locations.length === 0
              ? "No testing locations"
              : "No matching locations"
          }
          description={
            directory.locations.length === 0
              ? "Create a physical room or remote lab before scheduling sessions."
              : "Change or reset the current filters."
          }
          action={
            directory.locations.length === 0 ? (
              <CreateTestingLocationDialog />
            ) : null
          }
        />
      ) : (
        <>
          <div className="hidden overflow-hidden rounded-md border md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Location</TableHead>
                  <TableHead>Mode</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Capacity</TableHead>
                  <TableHead>Contact</TableHead>
                  <TableHead className="w-[13rem] text-right">
                    Actions
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {locations.map((location) => (
                  <TableRow key={location.id}>
                    <TableCell className="min-w-[18rem] whitespace-normal">
                      <div className="font-medium">{location.name}</div>
                      <div className="mt-1 line-clamp-2 text-xs text-muted-foreground">
                        {getLocationPlace(location)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <span className="inline-flex items-center gap-2">
                        {location.isVirtual ? (
                          <Globe2 aria-hidden="true" className="size-4" />
                        ) : (
                          <Building2 aria-hidden="true" className="size-4" />
                        )}
                        {location.isVirtual ? "Remote" : "Physical"}
                      </span>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={location.isDeleted ? "secondary" : "outline"}
                      >
                        {location.isDeleted
                          ? "Archived"
                          : normalizeTestingLocationStatus(location.status)}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-3 text-xs">
                        <span className="inline-flex items-center gap-1">
                          <Users aria-hidden="true" className="size-3.5" />
                          {location.maxTestersCapacity ?? 0}
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <Gamepad2 aria-hidden="true" className="size-3.5" />
                          {location.maxProjectsCapacity ?? 0}
                        </span>
                      </div>
                    </TableCell>
                    <TableCell className="max-w-56 whitespace-normal">
                      {location.contactEmail ? (
                        <span className="inline-flex items-center gap-1 text-xs">
                          <Mail aria-hidden="true" className="size-3.5" />
                          {location.contactEmail}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">
                          Not configured
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <LocationActions location={location} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="grid gap-3 md:hidden">
            {locations.map((location) => (
              <article key={location.id} className="rounded-md border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="truncate font-semibold">{location.name}</h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {getLocationPlace(location)}
                    </p>
                  </div>
                  <Badge variant={location.isDeleted ? "secondary" : "outline"}>
                    {location.isDeleted
                      ? "Archived"
                      : normalizeTestingLocationStatus(location.status)}
                  </Badge>
                </div>
                <div className="mt-4 flex flex-wrap gap-x-4 gap-y-2 text-sm">
                  <span className="inline-flex items-center gap-1.5">
                    {location.isVirtual ? (
                      <Globe2 aria-hidden="true" className="size-4" />
                    ) : (
                      <Building2 aria-hidden="true" className="size-4" />
                    )}
                    {location.isVirtual ? "Remote" : "Physical"}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Users aria-hidden="true" className="size-4" />
                    {location.maxTestersCapacity ?? 0} testers
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Gamepad2 aria-hidden="true" className="size-4" />
                    {location.maxProjectsCapacity ?? 0} projects
                  </span>
                </div>
                <div className="mt-4 border-t pt-3">
                  <LocationActions location={location} />
                </div>
              </article>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
