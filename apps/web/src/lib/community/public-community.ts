/** View model for published projects; populated by `getPublishedProjects` from the Projects API. */
export interface PublicProject {
  slug: string;
  title: string;
  creatorId?: string;
  creator: string;
  creatorRole: string;
  summary: string;
  description: string;
  status: string;
  tags: string[];
  coursePath: string;
  accent: string;
  previewImage: string;
  buildType: string;
  feedbackGoal: string;
  feedbackCount?: number;
  metrics: Array<{ label: string; value: string }>;
  media: Array<{ label: string; detail: string }>;
}

export interface PublicMemberSpotlight {
  id?: string;
  name: string;
  handle: string;
  role: string;
  focus: string;
  contribution: string;
}

export interface PublicPlaytest {
  title: string;
  date: string;
  format: string;
  seats: string;
  href: string;
}

export interface PublicActivity {
  actor: string;
  action: string;
  target: string;
  href: string;
}

export const communityOpportunities = [
  {
    title: 'Playtest mentor',
    type: 'Volunteer',
    commitment: '2 hours/week',
    description: 'Guide testers toward useful feedback and summarize findings for project teams.',
  },
  {
    title: 'Course project reviewer',
    type: 'Community role',
    commitment: 'Async',
    description: 'Review student milestones and help turn course work into portfolio proof.',
  },
  {
    title: 'Launch checklist contributor',
    type: 'Open source',
    commitment: 'Issue-based',
    description: 'Improve publishing templates, platform notes, and release-readiness checklists.',
  },
] as const;

