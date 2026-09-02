import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

export function TestingLabPageHeader({
  icon: Icon,
  title,
  description,
  actions,
  navigation,
  headingLevel = 1,
  bordered = true,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  actions?: ReactNode;
  navigation?: ReactNode;
  headingLevel?: 1 | 2;
  bordered?: boolean;
}) {
  const Heading = headingLevel === 2 ? 'h2' : 'h1';

  return (
    <header className={`space-y-4 ${bordered ? 'border-b pb-4' : ''}`}>
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 items-start gap-3">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted/60">
            <Icon className="size-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <Heading className="text-2xl font-semibold">{title}</Heading>
            <p className="mt-1 max-w-3xl text-sm text-muted-foreground">{description}</p>
          </div>
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
      {navigation ? <div className="min-w-0">{navigation}</div> : null}
    </header>
  );
}
