import { GradingScenarioLab } from "@/components/learning/grading-validation/grading-scenario-lab";
import { getGradingScenarioLabAccess } from "@/lib/grading-scenarios/access.server";
import { getGradingScenarioLabModel } from "@/lib/grading-scenarios/service.server";
import { Badge } from "@game-guild/ui/components/badge";
import { FlaskConical } from "lucide-react";
import { notFound } from "next/navigation";
import type React from "react";

export const dynamic = "force-dynamic";
export const runtime = "nodejs";

export default async function Page(): Promise<React.JSX.Element> {
  const access = await getGradingScenarioLabAccess();
  if (!access.allowed) notFound();

  const model = await getGradingScenarioLabModel();
  return (
    <div className="flex flex-col gap-6 p-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-md border bg-muted">
            <FlaskConical className="size-5" />
          </div>
          <div>
            <h1 className="text-2xl font-semibold">Grading validation</h1>
            <p className="text-sm text-muted-foreground">
              Scenario adapters and persisted checkpoints
            </p>
          </div>
        </div>
        <Badge variant="outline">Local only</Badge>
      </header>
      <GradingScenarioLab initialModel={model} />
    </div>
  );
}
