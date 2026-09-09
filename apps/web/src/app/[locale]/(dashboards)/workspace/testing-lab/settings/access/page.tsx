import { TestingLabAccessManagement } from "@/components/testing-lab/testing-lab-access-management";
import { TestingLabConfirmAction } from "@/components/testing-lab/testing-lab-confirm-action";
import {
  CreateTestingLabRoleDialog,
  EditTestingLabRoleDialog,
} from "@/components/testing-lab/testing-lab-dialogs";
import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import {
  TestingLabAccessIssues,
  TestingLabEmptyState,
} from "@/components/testing-lab/testing-lab-state";
import { deleteTestingLabRole } from "@/lib/testing-lab/actions";
import { getMembers } from "@/lib/community/queries/members";
import {
  getTestingLabAdministration,
  getTestingLabDashboard,
} from "@/lib/testing-lab";
import { Badge } from "@game-guild/ui/components/badge";
import { KeyRound, ShieldCheck, Users } from "lucide-react";

function permissionLabel(permission: string) {
  return permission
    .replace(/^can/, "")
    .replace(/([A-Z])/g, " $1")
    .trim();
}

export default async function TestingLabAccessPage() {
  const [administration, memberDirectory, directory] = await Promise.all([
    getTestingLabAdministration(),
    getMembers({ page: 1, limit: 100 }),
    getTestingLabDashboard(),
  ]);
  const resources = [
    ...directory.requests.map((request) => ({
      id: request.id,
      label: request.title,
      type: "TestingRequest" as const,
    })),
    ...directory.sessions.map((session) => ({
      id: session.id,
      label: session.sessionName,
      type: "TestingSession" as const,
    })),
    ...directory.locations.map((location) => ({
      id: location.id,
      label: location.name,
      type: "TestingLocation" as const,
    })),
  ];

  return (
    <div className="space-y-7 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={ShieldCheck}
        title="Access and roles"
        description="Manage reusable Testing Lab roles and time-limited resource exceptions for community members."
        actions={<CreateTestingLabRoleDialog />}
      />
      <TestingLabAccessIssues
        issues={[...administration.accessIssues, ...directory.accessIssues]}
      />

      <section className="grid gap-5 border-b pb-7 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <div>
          <div className="flex items-center gap-2">
            <Users
              aria-hidden="true"
              className="size-4 text-muted-foreground"
            />
            <h2 className="font-semibold">Member access</h2>
          </div>
          <p className="mt-2 text-sm text-muted-foreground">
            Select a member to inspect merged permissions, assign a role, or
            grant a resource exception.
          </p>
        </div>
        <div className="max-w-2xl">
          <TestingLabAccessManagement
            members={memberDirectory.members.map((member) => ({
              id: member.id,
              label: member.displayName + " · " + member.email,
            }))}
            roles={administration.roles}
            resources={resources}
          />
          <p className="mt-2 text-xs text-muted-foreground">
            Showing the first {memberDirectory.members.length} members. Use
            member search when the directory exceeds this list.
          </p>
        </div>
      </section>

      <section className="space-y-4">
        <div className="flex items-start gap-3">
          <KeyRound
            aria-hidden="true"
            className="mt-0.5 size-4 text-muted-foreground"
          />
          <div>
            <h2 className="font-semibold">Role templates</h2>
            <p className="text-sm text-muted-foreground">
              Templates establish baseline capabilities. Assigned templates
              expand into effective permissions automatically.
            </p>
          </div>
        </div>

        {administration.roles.length === 0 ? (
          <TestingLabEmptyState
            title="No Testing Lab roles"
            description="Create a role template that matches how facilitators, moderators, and reviewers operate."
            action={<CreateTestingLabRoleDialog />}
          />
        ) : (
          <div className="divide-y overflow-hidden rounded-md border">
            {administration.roles.map((role) => {
              const permissions = Object.entries(role.permissions ?? {})
                .filter(([, enabled]) => enabled)
                .map(([permission]) => permission);

              return (
                <article
                  key={role.id ?? role.name}
                  className="grid gap-4 p-4 lg:grid-cols-[minmax(12rem,0.8fr)_minmax(18rem,1.5fr)_auto] lg:items-center"
                >
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-semibold">{role.name}</h3>
                      {role.isSystemRole ? (
                        <Badge variant="secondary">System</Badge>
                      ) : (
                        <Badge variant="outline">Custom</Badge>
                      )}
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {role.description ?? "No description."}
                    </p>
                  </div>

                  <div>
                    <p className="mb-2 text-xs font-medium uppercase text-muted-foreground">
                      {permissions.length} permissions
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                      {permissions.length === 0 ? (
                        <span className="text-sm text-muted-foreground">
                          No permissions enabled.
                        </span>
                      ) : (
                        permissions.map((permission) => (
                          <Badge key={permission} variant="outline">
                            {permissionLabel(permission)}
                          </Badge>
                        ))
                      )}
                    </div>
                  </div>

                  {!role.isSystemRole ? (
                    <div className="flex items-center gap-2 lg:justify-end">
                      <EditTestingLabRoleDialog role={role} />
                      <TestingLabConfirmAction
                        action={deleteTestingLabRole}
                        fields={{ idOrName: role.id ?? role.name ?? "" }}
                        label="Delete"
                        title={"Delete " + role.name + "?"}
                        description="Only an unassigned custom role can be deleted. Remove its member assignments first."
                        confirmLabel="Delete role"
                        intent="delete"
                      />
                    </div>
                  ) : (
                    <span className="text-xs text-muted-foreground lg:text-right">
                      Managed by the platform
                    </span>
                  )}
                </article>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}
