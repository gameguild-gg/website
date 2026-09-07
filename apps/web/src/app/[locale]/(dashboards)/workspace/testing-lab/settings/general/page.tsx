import { TestingLabActionForm } from "@/components/testing-lab/testing-lab-action-form";
import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import { TestingLabAccessIssues } from "@/components/testing-lab/testing-lab-state";
import {
  resetTestingLabSettings,
  updateTestingLabSettings,
} from "@/lib/testing-lab/actions";
import { getTestingLabAdministration } from "@/lib/testing-lab";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import { Switch } from "@game-guild/ui/components/switch";
import { Textarea } from "@game-guild/ui/components/textarea";
import { Bell, CalendarClock, Settings, UserCheck } from "lucide-react";

const operatingControls = [
  {
    name: "allowPublicSignups",
    label: "Public tester registration",
    description:
      "Allow authenticated community members to register for sessions that have open capacity.",
    icon: UserCheck,
  },
  {
    name: "requireApproval",
    label: "Project approval required",
    description:
      "Keep project applications pending until a manager or review committee approves them.",
    icon: CalendarClock,
  },
  {
    name: "enableNotifications",
    label: "Operational notifications",
    description:
      "Send status, schedule, waitlist, and feedback reminders to participants and managers.",
    icon: Bell,
  },
] as const;

export default async function TestingLabSettingsPage() {
  const administration = await getTestingLabAdministration();
  const settings = administration.settings;
  const enabledByName = {
    allowPublicSignups: settings?.allowPublicSignups,
    requireApproval: settings?.requireApproval,
    enableNotifications: settings?.enableNotifications,
  };

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={Settings}
        title="General settings"
        description="Set the identity, scheduling defaults, and participation policy used across this Testing Lab."
      />
      <TestingLabAccessIssues issues={administration.accessIssues} />

      <TestingLabActionForm
        action={updateTestingLabSettings}
        secondaryAction={resetTestingLabSettings}
        submitLabel="Save settings"
        secondaryLabel="Reset defaults"
        className="max-w-5xl space-y-8"
        actionsClassName="sticky bottom-0 z-10 flex flex-wrap justify-between gap-3 border-t bg-background/95 py-4 backdrop-blur"
      >
        <section className="grid gap-5 border-b pb-8 lg:grid-cols-[14rem_minmax(0,1fr)]">
          <div>
            <h2 className="font-semibold">Identity</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              The name and context managers see across Testing Lab operations.
            </p>
          </div>
          <div className="grid gap-4">
            <div className="space-y-2">
              <Label htmlFor="lab-name">Lab name</Label>
              <Input
                id="lab-name"
                name="labName"
                defaultValue={settings?.labName ?? "GameGuild Testing Lab"}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lab-description">Description</Label>
              <Textarea
                id="lab-description"
                name="description"
                rows={3}
                defaultValue={settings?.description ?? ""}
              />
            </div>
            <div className="max-w-sm space-y-2">
              <Label htmlFor="lab-timezone">Timezone</Label>
              <Input
                id="lab-timezone"
                name="timezone"
                defaultValue={settings?.timezone ?? "UTC"}
                required
              />
              <p className="text-xs text-muted-foreground">
                IANA timezone used for schedules, reminders, and reports.
              </p>
            </div>
          </div>
        </section>

        <section className="grid gap-5 border-b pb-8 lg:grid-cols-[14rem_minmax(0,1fr)]">
          <div>
            <h2 className="font-semibold">Scheduling defaults</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Starting values for new sessions. Managers can adjust each session
              later.
            </p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="lab-duration">Session duration</Label>
              <div className="relative">
                <Input
                  id="lab-duration"
                  name="defaultSessionDuration"
                  type="number"
                  min="15"
                  step="15"
                  defaultValue={settings?.defaultSessionDuration ?? 120}
                  className="pr-20"
                />
                <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground">
                  minutes
                </span>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="lab-concurrency">Simultaneous sessions</Label>
              <Input
                id="lab-concurrency"
                name="maxSimultaneousSessions"
                type="number"
                min="1"
                defaultValue={settings?.maxSimultaneousSessions ?? 4}
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="lab-reminders">Reminder days before event</Label>
              <Input
                id="lab-reminders"
                name="reminderDaysBefore"
                placeholder="4,2,1"
                defaultValue={settings?.reminderDaysBefore ?? "4,2,1"}
                aria-describedby="lab-reminders-help"
              />
              <p id="lab-reminders-help" className="text-xs text-muted-foreground">
                Comma-separated day thresholds when reminders are sent to the event
                manager and approved testers. Events can override this.
              </p>
            </div>
          </div>
        </section>

        <section className="grid gap-5 lg:grid-cols-[14rem_minmax(0,1fr)]">
          <div>
            <h2 className="font-semibold">Participation policy</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Tenant-wide controls for applications, attendance, and
              communication.
            </p>
          </div>
          <div className="divide-y rounded-md border">
            <div className="space-y-2 p-4">
              <Label htmlFor="version-submission-policy">Eligible project versions</Label>
              <select
                id="version-submission-policy"
                name="versionSubmissionPolicy"
                defaultValue={settings?.versionSubmissionPolicy ?? "ReadyMutableUntilReview"}
                className="flex h-9 w-full max-w-xl rounded-md border border-input bg-background px-3 text-sm"
              >
                <option value="ReadyMutableUntilReview">Ready for Testing or Released; replace while Pending</option>
                <option value="ReleasedImmutable">Released only; immutable after first submission</option>
              </select>
              <p className="text-xs text-muted-foreground">Changes apply to new submissions and future replacements. Approved applications keep their recorded policy.</p>
            </div>
            {operatingControls.map((control) => {
              const Icon = control.icon;
              return (
                <div
                  key={control.name}
                  className="flex items-start justify-between gap-4 p-4"
                >
                  <div className="flex gap-3">
                    <Icon
                      aria-hidden="true"
                      className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                    />
                    <div>
                      <Label htmlFor={"setting-" + control.name}>
                        {control.label}
                      </Label>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {control.description}
                      </p>
                    </div>
                  </div>
                  <Switch
                    id={"setting-" + control.name}
                    name={control.name}
                    value="true"
                    defaultChecked={Boolean(enabledByName[control.name])}
                    aria-label={control.label}
                  />
                </div>
              );
            })}
          </div>
        </section>
      </TestingLabActionForm>
    </div>
  );
}
