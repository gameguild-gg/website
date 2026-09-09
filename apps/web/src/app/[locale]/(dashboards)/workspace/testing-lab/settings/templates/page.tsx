import { TestingEventTemplateManagement } from '@/components/testing-lab/testing-event-template-management';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues } from '@/components/testing-lab/testing-lab-state';
import { getTestingEventTemplates } from '@/lib/testing-lab/events-queries';
import { Files } from 'lucide-react';

export default async function TestingLabTemplateSettingsPage() {
  const directory = await getTestingEventTemplates(true);
  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={Files}
        title="Event templates"
        description="Version reusable rules, instructions, forms, and defaults for new Testing Lab events."
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />
      <TestingEventTemplateManagement templates={directory.templates} />
    </div>
  );
}
