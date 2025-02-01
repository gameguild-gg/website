'use server';

export type Project = {
  id: string
  slug: string
  name: string
  status: 'not-started' | 'in-progress' | 'active' | 'on-hold'
  createdAt?: string
  updatedAt?: string
}

let projects: Project[] = [
  { id: '1', slug: 'website-redesign', name: 'Website Redesign', status: 'in-progress' },
  { id: '2', slug: 'mobile-app-development', name: 'Mobile App Development', status: 'not-started' },
  { id: '3', slug: 'e-commerce-platform', name: 'E-commerce Platform', status: 'active' },
  { id: '4', slug: 'crm-integration', name: 'CRM Integration', status: 'on-hold' },
  { id: '5', slug: 'data-analytics-dashboard', name: 'Data Analytics Dashboard', status: 'not-started' },
  { id: '6', slug: 'cloud-migration', name: 'Cloud Migration', status: 'active' },
  { id: '7', slug: 'ai-chatbot-implementation', name: 'AI Chatbot Implementation', status: 'in-progress' },
  { id: '8', slug: 'cybersecurity-upgrade', name: 'Cybersecurity Upgrade', status: 'active' },
  { id: '9', slug: 'iot-device-management', name: 'IoT Device Management', status: 'not-started' },
  { id: '10', slug: 'blockchain-prototype', name: 'Blockchain Prototype', status: 'on-hold' },
];

export async function getProjects() {
  return projects;
}

export async function getProjectBySlug(slug: string) {
  return projects.find(project => project.slug === slug);
}

export async function updateProject(project: Project) {
  const index = projects.findIndex(p => p.id === project.id);
  projects[index] = project;
}